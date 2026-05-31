using NServiceBus;

namespace Middleware.Contracts.Commands;

public sealed class RetrieveAppraisalDocumentCommand : ICommand
{
    public string RequestId { get; set; } = string.Empty;

    public string DocumentKey { get; set; } = string.Empty;

    public string SourceSystem { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; set; }
}
