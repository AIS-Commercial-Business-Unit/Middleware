using Middleware.Contracts.Models;
using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class Uc4MainframeDocumentListCompletedEvent : IEvent
{
    public string RequestId { get; set; } = string.Empty;

    public List<Uc4DocumentSummary> Documents { get; set; } = new();
}
