using System.ComponentModel.DataAnnotations;
using OpenAI.Assistants;

namespace AllaganAssistantNode.Data.Models;

public class ThreadRecord
{
    [Key]
    public string Id { get; set; }

    public ThreadRecord(AssistantThread thread)
    {
        Id = thread.Id;
    }

    public ThreadRecord() {}
}