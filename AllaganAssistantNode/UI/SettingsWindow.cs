using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AllaganAssistantNode.UI;

public class SettingsWindow : Window
{
    public SettingsWindow() : base("Allagan Assistant Node Settings", ImGuiWindowFlags.None, false)
    {
        Size = new Vector2(200, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    string _key = AllaganAssistantNode.Configuration.OpenAIKey;
    public override void Draw()
    {
        ImGui.InputText("OpenAI API Key", ref _key, 1000);
        if (ImGui.Button("Save"))
        {
            AllaganAssistantNode.Configuration.OpenAIKey = _key;
            AllaganAssistantNode.Configuration.Save();
        }
    }
}