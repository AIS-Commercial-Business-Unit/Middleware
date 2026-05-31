using NServiceBus;

namespace Middleware.Contracts.Commands;

public sealed class GetAppraisalDocumentListCommand : ICommand
{
    public string RequestId { get; set; } = string.Empty;

    public string PolicyNumber { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; set; }
}
