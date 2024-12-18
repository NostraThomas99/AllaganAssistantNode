using System.Collections.Concurrent;
using AllaganAssistantNode.Controllers;
using AllaganAssistantNode.Data;
using AllaganAssistantNode.Data.Models;
using AllaganAssistantNode.UI;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Commands;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using Microsoft.EntityFrameworkCore;
using OpenAI.Assistants;
using OpenAI.Chat;

namespace AllaganAssistantNode;

public class AllaganAssistantNode : IDalamudPlugin
{
    public static Configuration Configuration;
    public static FunctionToolController FunctionToolController;
    public static BackgroundTaskController BackgroundTaskController;

    public WindowSystem WindowSystem;

    public static MainWindow MainWindow;
    public static SettingsWindow SettingsWindow;

    public AICache<MessageRecord, ThreadMessage> MessageRecordCache;
    public AICache<AssistantRecord, Assistant> AssistantRecordCache;
    public AICache<RunRecord, ThreadRun> RunRecordCache;
    public AICache<ThreadRecord, OpenAI.Assistants.AssistantThread> ThreadRecordCache;
    private AssistantClient? _assistantClient;
    private string _openAiKey;

    public AllaganAssistantNode(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        FunctionToolController = new FunctionToolController();
        BackgroundTaskController = new BackgroundTaskController();
        Configuration = Configuration.Load();
        WindowSystem = new WindowSystem();

        MessageRecordCache = new AICache<MessageRecord, ThreadMessage>(GetThreadMessage, record => record.Id, TimeSpan.FromMinutes(5));
        AssistantRecordCache = new AICache<AssistantRecord, Assistant>(GetAssistant, record => record.Id, TimeSpan.FromMinutes(1));
        RunRecordCache = new AICache<RunRecord, ThreadRun>(GetThreadRun, record => record.Id, TimeSpan.FromSeconds(30));
        ThreadRecordCache = new AICache<ThreadRecord, OpenAI.Assistants.AssistantThread>(GetThread, record => record.Id, TimeSpan.FromMinutes(2));

        MainWindow = new MainWindow(this);
        SettingsWindow = new SettingsWindow();

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(SettingsWindow);

        Svc.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += ToggleSettingsWindow;
        Svc.PluginInterface.UiBuilder.OpenMainUi += ToggleMainWindow;
        Svc.Framework.Update += WorkNonterminalRuns;
    }

    private ThreadMessage? GetThreadMessage(MessageRecord messageRecord)
    {
        var message = AssistantClient?.GetMessage(messageRecord.ThreadId, messageRecord.Id);
        if (message != null) return message;
        return null;
    }

    private Assistant? GetAssistant(AssistantRecord record)
    {
        var assistant = AssistantClient?.GetAssistant(record.Id);
        if (assistant != null) return assistant;
        return null;
    }

    private ThreadRun? GetThreadRun(RunRecord runRecord)
    {
        var run = AssistantClient?.GetRun(runRecord.ThreadId, runRecord.Id);
        if (run != null) return run;
        return null;
    }

    private OpenAI.Assistants.AssistantThread? GetThread(ThreadRecord threadRecord)
    {
        var thread = AssistantClient?.GetThread(threadRecord.Id);
        if (thread != null) return thread;
        return null;
    }

    public Task CreateAssistant()
    {
        AssistantCreationOptions assistantCreationOptions = new AssistantCreationOptions()
        {
            Description = $"Allagan Assistant Node Plugin Assistant created at {DateTime.UtcNow} (UTC)",
            Name = "Allagan Assistant Node",
            Instructions =
                "You are Allagan Assistant Node, a plugin for the video game Final Fantasy 14 designed to assist players with in-game tasks. " +
                "You have access to a variety of tools to interact with the game and should take advantage of them whenever possible. " +
                "Avoid using markdown and other styling in your response and keep output as close to plaintext as possible. " +
                "Always take the initiative on doing tool calls to retrieve data, do not ask the user for permission first. " +
                "Avoid being too analytical in your responses. If your tool call returns a JSON object, make an effort to avoid structuring your output in the same way. All responses should be conversational and friendly instead of analytical.",
            Tools =
            {
                FunctionToolController.PlayerDataTool,
                FunctionToolController.ActiveFateTool,
                FunctionToolController.MoveToLocationTool,
                FunctionToolController.GetNearbyGameObjectsTool,
                FunctionToolController.SpecificGameObjectDataTool
            }
        };
        var assistant = AssistantClient?.CreateAssistant("gpt-4o-mini", assistantCreationOptions);
        if (assistant != null)
        {
            var record = new AssistantRecord(assistant);
            Configuration.AssistantRecords.Add(record);
            Configuration.Save();
        }
        return Task.CompletedTask;
    }

    public Task CreateThread(AssistantRecord assistant)
    {
        var options = new ThreadCreationOptions()
        {
        };
        var thread = AssistantClient?.CreateThread(options);
        if (thread != null)
        {
            var record = new ThreadRecord(thread);
            Configuration.ThreadRecords.Add(record);
            Configuration.Save();
        }
        return Task.CompletedTask;
    }

    private readonly Queue<ThreadRun> ThreadRunQueue = new();

    private void WorkNonterminalRuns(IFramework framework)
    {
        if (ThreadRunQueue.Count > 0)
        {
            var run = ThreadRunQueue.Dequeue();
            BackgroundTaskController.EnqueueTask(() => ProcessThreadRun(run));
        }
    }

    private Task ProcessThreadRun(ThreadRun run)
    {
        var newRun = AssistantClient?.GetRun(run.ThreadId, run.Id);
        if (newRun == null)
            return Task.CompletedTask;

        if (!newRun.Value.Status.IsTerminal)
        {
            Svc.Log.Verbose($"Thread {run.ThreadId} is not terminal");
            if (newRun.Value.Status == RunStatus.RequiresAction)
            {
                Svc.Log.Verbose($"Thread {run.ThreadId} requires local action");
                List<ToolOutput> output = new List<ToolOutput>();
                foreach (var action in newRun.Value.RequiredActions)
                {
                    var returnString = FunctionToolController.DoRunWork(action);
                    var toolOutput = new ToolOutput(action.ToolCallId, returnString);
                    output.Add(toolOutput);
                }

                newRun = AssistantClient?.SubmitToolOutputsToRun(newRun.Value.ThreadId, newRun.Value.Id, output);
                if (newRun == null)
                    return Task.CompletedTask;
            }

            ThreadRunQueue.Enqueue(newRun.Value);
            return Task.CompletedTask;
        }
        else
        {
            var messages = AssistantClient?.GetMessages(run.ThreadId);
            if (messages == null)
                return Task.CompletedTask;
            foreach (var message in messages)
            {
                if (Configuration.MessageRecords.Any(x => x.Id == message.Id))
                    continue;
                var messageRecord = new MessageRecord(message);
                Configuration.MessageRecords.Add(messageRecord);
            }
            Configuration.Save();
            return Task.CompletedTask;
        }
    }

    public Task CreateRun(AssistantRecord assistant, ThreadRecord threadRecord)
    {
        var run = AssistantClient?.CreateRun(threadRecord.Id, assistant.Id);
        if (run != null)
        {
            var record = new RunRecord(run);
            Configuration.RunRecords.Add(record);
            Configuration.Save();
            ThreadRunQueue.Enqueue(run);
        }
        return Task.CompletedTask;
    }

    public Queue<ThreadRecord> ThreadsNeedingMessages = new();

    [Cmd("/aan", "Main Command")]
    private void OnCommand(string command, string args)
    {
        if (string.Equals(args, "settings"))
        {
            ToggleSettingsWindow();
        }
        else
        {
            ToggleMainWindow();
        }
    }

    public void ToggleSettingsWindow()
    {
        SettingsWindow.IsOpen = !SettingsWindow.IsOpen;
    }

    public void ToggleMainWindow()
    {
        MainWindow.IsOpen = !MainWindow.IsOpen;
    }

    public void AddNewChatWindow(ThreadRecord thread)
    {
        var chatWindow = new ChatWindow(thread, this)
        {
            IsOpen = true
        };
        WindowSystem.AddWindow(chatWindow);
    }

    public Task AddUserMessage(ThreadRecord thread, string message)
    {
        var assistant = Configuration.AssistantRecords.FirstOrDefault();
        if (assistant != null)
        {
            var content = MessageContent.FromText(message);
            var messageAdd = AssistantClient?.CreateMessage(thread.Id, MessageRole.User, [content]);
            if (messageAdd != null)
            {
                var messageRecord = new MessageRecord(messageAdd);
                Configuration.MessageRecords.Add(messageRecord);
                Configuration.Save();
                CreateRun(assistant, thread);
            }
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        ECommonsMain.Dispose();
    }
    
    private AssistantClient? AssistantClient
    {
        get
        {
            if (string.IsNullOrEmpty(AllaganAssistantNode.Configuration.OpenAIKey))
            {
                Svc.Log.Verbose("OpenAIKey is null or empty");
                return null;
            }

            if (_assistantClient == null || _openAiKey != AllaganAssistantNode.Configuration.OpenAIKey)
            {
                Svc.Log.Verbose("API key has changed. Reloading client.");
                _openAiKey = AllaganAssistantNode.Configuration.OpenAIKey;
                _assistantClient = new AssistantClient(_openAiKey);
            }

            return _assistantClient;
        }
    }

    public void ClearData()
    {
        Configuration.MessageRecords.Clear();
        Configuration.ThreadRecords.Clear();
        Configuration.AssistantRecords.Clear();
        Configuration.RunRecords.Clear();
        Configuration.Save();
    }
}