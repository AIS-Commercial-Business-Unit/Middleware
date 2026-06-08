using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class FileRetrievedDetectedEvent : IEvent
{
    public string BatchId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
}
