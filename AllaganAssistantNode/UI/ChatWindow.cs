using System.Numerics;
using AllaganAssistantNode.Data;
using AllaganAssistantNode.Data.Models;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.Havok.Common.Serialize.Util;
using ImGuiNET;

namespace AllaganAssistantNode.UI;

public class ChatWindow : Window
{
    public ThreadRecord Thread;
    private readonly AllaganAssistantNode _plugin;
    public ChatWindow(ThreadRecord thread, AllaganAssistantNode plugin) : base($"Chat##{thread.Id}")
    {
        _plugin = plugin;
        Thread = thread;
        Size = new Vector2(200, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        DrawChatMessages();
        ImGui.Separator();
        DrawNewMessage();
    }

    public override void OnClose()
    {
        _plugin.WindowSystem.RemoveWindow(this);
        base.OnClose();
    }

    private string _newMessage = string.Empty;
    private void DrawNewMessage()
    {
        ImGui.InputText("##NewMessage", ref _newMessage, 256);
        ImGui.SameLine();
        if (ImGui.Button("Send"))
        {
            var messageText = _newMessage;
            AllaganAssistantNode.BackgroundTaskController.EnqueueTask(() => _plugin.AddUserMessage(Thread, messageText));
            _newMessage = string.Empty;
        }
    }

    private void DrawChatMessages()
    {
        ImGui.BeginChild("ChatMessages", new Vector2(0, ImGui.GetContentRegionAvail().Y * 0.95f), true);

        var messages = AllaganAssistantNode.Configuration.MessageRecords.Where(m => m.ThreadId == Thread.Id);
        foreach (var message in messages)
        {
            // Calculate the height dynamically
            float separatorHeight = ImGui.GetStyle().ItemSpacing.Y + ImGui.GetStyle().FramePadding.Y; // Add some padding for the separator.
            float messageContentHeight;

            if (_plugin.MessageRecordCache.TryGetValue(message, out var threadMessage))
            {
                messageContentHeight = ImGui.CalcTextSize(threadMessage?.Content[0].Text, true, ImGui.GetContentRegionAvail().Y).Y;
            }
            else
            {
                messageContentHeight = ImGui.CalcTextSize("Loading...").Y; // For the "Loading..." fallback text.
            }

            // Add up the heights
            float totalHeight = ImGui.CalcTextSize(threadMessage?.Role.ToString()).Y + separatorHeight + messageContentHeight + 25; // Add some extra padding for clarity.

            // Use the total height for the child
            ImGui.BeginChild($"Message##{message.Id}", new Vector2(0, totalHeight), true);

            // Render the contents
            if (threadMessage != null)
            {
                ImGui.Text($"{threadMessage.Role}:");
                ImGui.Separator();
                ImGui.TextWrapped(threadMessage.Content[0].Text);
            }
            else
            {
                ImGui.TextWrapped("Loading...");
            }

            ImGui.EndChild();
        }

        ImGui.EndChild();
    }
}