using Middleware.Contracts.Models;
using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class AtWorkDocumentListCompletedEvent : IEvent
{
    public string RequestId { get; set; } = string.Empty;
    public List<AppraisalDocumentSummary> Documents { get; set; } = new();
}
