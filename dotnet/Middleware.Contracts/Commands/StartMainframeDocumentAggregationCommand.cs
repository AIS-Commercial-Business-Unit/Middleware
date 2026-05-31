using NServiceBus;

namespace Middleware.Contracts.Commands;

public sealed class StartMainframeDocumentAggregationCommand : ICommand
{
    public string RequestId { get; set; } = string.Empty;

    public string DocumentKey { get; set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; set; }
}
