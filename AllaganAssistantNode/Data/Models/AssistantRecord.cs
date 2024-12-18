using System.ComponentModel.DataAnnotations;
using OpenAI.Assistants;

namespace AllaganAssistantNode.Data.Models;

public class AssistantRecord
{
    [Key]
    public string Id { get; set; }

    public AssistantRecord()
    {
    }

    public AssistantRecord(Assistant assistant)
    {
        Id = assistant.Id;
    }
}