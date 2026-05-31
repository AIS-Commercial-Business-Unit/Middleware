using NServiceBus;

namespace Middleware.Contracts.Messages;

public sealed class RetrieveAppraisalDocumentResponse : IMessage
{
    public string RequestId { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentKey { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ContentBase64 { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
