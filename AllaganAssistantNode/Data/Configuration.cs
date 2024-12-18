using AllaganAssistantNode.Data.Models;
using Dalamud.Configuration;
using ECommons.DalamudServices;
using Newtonsoft.Json;

namespace AllaganAssistantNode.Data;

public class Configuration : IPluginConfiguration
{
    public static Configuration Load()
    {
        var file = Svc.PluginInterface.ConfigFile;
        if (!file.Exists)
            return new Configuration();
        var json = File.ReadAllText(file.FullName);
        var obj = JsonConvert.DeserializeObject<Configuration>(json);
        if (obj == null)
            return new Configuration();
        return obj;
    }

    public void Save()
    {
        var file = Svc.PluginInterface.ConfigFile;
        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(file.FullName, json);
    }
    public int Version { get; set; } = 1;
    public string OpenAIKey { get; set; } = string.Empty;

    public List<AssistantRecord> AssistantRecords { get; set; } = new List<AssistantRecord>();
    public List<MessageRecord> MessageRecords { get; set; } = new List<MessageRecord>();
    public List<RunRecord> RunRecords { get; set; } = new List<RunRecord>();
    public List<ThreadRecord> ThreadRecords { get; set; } = new List<ThreadRecord>();
}