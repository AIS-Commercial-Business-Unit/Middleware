using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class AtWorkDocumentRetrievedEvent : IEvent
{
    public string RequestId { get; set; } = string.Empty;

    public string PolicyNumber { get; set; } = string.Empty;

    public string DocumentKey { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;
}
