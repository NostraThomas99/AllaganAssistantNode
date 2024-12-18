using AllaganAssistantNode.Data.Models;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;

namespace AllaganAssistantNode.UI;

public class ChatSelector : ItemSelector<ThreadRecord>
{
    private readonly AllaganAssistantNode _plugin;
    public ChatSelector(IList<ThreadRecord> items, AllaganAssistantNode plugin) : base(items, Flags.Add | Flags.Delete)
    {
        _plugin = plugin;
    }

    protected override bool OnAdd(string name)
    {
        var assistant = AllaganAssistantNode.Configuration.AssistantRecords.FirstOrDefault();
        if (assistant == null)
            return false;
        AllaganAssistantNode.BackgroundTaskController.EnqueueTask(() => _plugin.CreateThread(assistant));
        return true;
    }

    protected override bool Filtered(int idx)
    {
        return false;
    }

    protected override bool OnDraw(int idx)
    {
        using var id = ImRaii.PushId(idx);
        return ImGui.Selectable(Items[idx].Id, idx == CurrentIdx);
    }
}