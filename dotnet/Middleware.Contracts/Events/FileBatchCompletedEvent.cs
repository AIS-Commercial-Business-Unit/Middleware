using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class FileBatchCompletedEvent : IEvent
{
    public string BatchId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public int SucceededRecords { get; set; }
    public int FailedRecords { get; set; }
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}
