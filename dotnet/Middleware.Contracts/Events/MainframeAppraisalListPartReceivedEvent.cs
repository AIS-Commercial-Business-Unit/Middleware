using Middleware.Contracts.Models;
using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class MainframeAppraisalListPartReceivedEvent : IEvent
{
    public string RequestId { get; set; } = string.Empty;

    public int SequenceNumber { get; set; }

    public int TotalExpected { get; set; }

    public Uc4DocumentSummary Document { get; set; } = new();
}
