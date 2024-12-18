using System.ComponentModel.DataAnnotations;
using OpenAI.Assistants;

namespace AllaganAssistantNode.Data.Models;

public class MessageRecord
{
    [Key]
    public string Id { get; set; }

    public string ThreadId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public MessageRecord(ThreadMessage threadMessage)
    {
        Id = threadMessage.Id;
        ThreadId = threadMessage.ThreadId;
        CreatedAt = threadMessage.CreatedAt.UtcDateTime;
    }

    public MessageRecord(){ }
}