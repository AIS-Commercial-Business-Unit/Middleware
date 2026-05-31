using Middleware.Contracts.Models;
using NServiceBus;

namespace Middleware.Contracts.Messages;

public sealed class GetAppraisalDocumentListResponse : IMessage
{
    public string RequestId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public List<Uc4DocumentSummary> Documents { get; set; } = new();
    public bool PartialResult { get; set; }
}
