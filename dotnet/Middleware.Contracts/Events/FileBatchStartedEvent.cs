using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class FileBatchStartedEvent : IEvent
{
    public string BatchId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
}
