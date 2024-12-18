using System.Numerics;
using AllaganAssistantNode.Data;
using AllaganAssistantNode.Data.Models;
using AllaganAssistantNode.Helpers;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OpenAI.Assistants;

namespace AllaganAssistantNode.UI;

public class MainWindow : Window
{
    private readonly ChatSelector _chatSelector;
    public MainWindow(AllaganAssistantNode allaganAssistantNode) : base("Allagan Assistant Node", ImGuiWindowFlags.None, false)
    {
        _plugin = allaganAssistantNode;
        _chatSelector = new ChatSelector(AllaganAssistantNode.Configuration.ThreadRecords, _plugin);
        Size = new Vector2(300, 700);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    private bool drawDebug = false;
    private readonly AllaganAssistantNode _plugin;
    public override void Draw()
    {
        ImGui.Text($"Assistant Chats");
        if (ImGui.Button("New Assistant"))
        {
            AllaganAssistantNode.BackgroundTaskController.EnqueueTask(() => _plugin.CreateAssistant());
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear Data"))
        {
            _plugin.ClearData();
        }

        _chatSelector.Draw(250);
        ImGui.SameLine();
        DrawChatDetails(_chatSelector.Current);
    }

    private void DrawChatDetails(ThreadRecord? chatSelectorCurrent)
    {
        ImGui.BeginChild("Chat Details", new Vector2(0, 0), true);
        if (chatSelectorCurrent is null)
        {
            ImGui.Text("No Chat Selected");
        }
        else
        {
            if (_plugin.ThreadRecordCache.TryGetValue(chatSelectorCurrent, out var thread))
            {
                if (thread is null)
                    return;
                if (ImGui.Button("Open Chat"))
                {
                    _plugin.AddNewChatWindow(chatSelectorCurrent);
                }
                ImGui.Text($"Thread ID: {thread.Id}");
                ImGui.Text($"Associated Runs: ");
                ImGui.Indent();
                foreach (var run in AllaganAssistantNode.Configuration.RunRecords.Where(
                             run => run.ThreadId == thread.Id))
                {
                    if (_plugin.RunRecordCache.TryGetValue(run, out var runRecord))
                    {
                        if (runRecord is null)
                            continue;
                        ImGui.Text($"Run ID: {runRecord.Id}");
                        ImGui.Indent();
                        DrawRunRecordDetails(runRecord);
                        ImGui.Unindent();
                    }
                    else
                    {
                        ImGui.Text($"Loading run...");
                    }
                }
            }
            else
            {
                ImGui.Text($"Loading thread...");
            }
        }
        ImGui.EndChild();
    }

    private void DrawRunRecordDetails(ThreadRun runRecord)
    {
        ImGui.Text($"IsTerminal {runRecord.Status.IsTerminal}");
        ImGui.Text($"Status: {runRecord.Status.ToString()}");
        ImGui.Text($"Model: {runRecord.Model}");
        ImGui.Text($"Input Tokens: {runRecord.Usage?.InputTokenCount}");
        ImGui.Text($"Output Tokens: {runRecord.Usage?.OutputTokenCount}");
    }
}