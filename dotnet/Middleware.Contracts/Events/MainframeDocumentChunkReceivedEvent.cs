using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class MainframeDocumentChunkReceivedEvent : IEvent
{
    public string RequestId { get; set; } = string.Empty;

    public string ChunkPayload { get; set; } = string.Empty;

    public bool IsFinal { get; set; }
}
