namespace Middleware.Contracts.Models;

public sealed class AppraisalDocumentSummary
{
    public string DocumentId { get; set; } = string.Empty;

    public string DocumentKey { get; set; } = string.Empty;

    public string SourceSystem { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentName { get; set; } = string.Empty;

    public string DocumentDate { get; set; } = string.Empty;

    public string PolicyNumber { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
