using System.ComponentModel.DataAnnotations;
using OpenAI.Assistants;

namespace AllaganAssistantNode.Data.Models;

public class RunRecord
{
    [Key]
    public string Id { get; set; }

    public string AssistantId { get; set; }

    public string ThreadId { get; set; }

    public RunRecord(ThreadRun threadRun)
    {
        Id = threadRun.Id;
        AssistantId = threadRun.AssistantId;
        ThreadId = threadRun.ThreadId;
    }

    public RunRecord() { }
}