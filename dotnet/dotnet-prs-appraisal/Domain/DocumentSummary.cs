namespace dotnet_prs_appraisal.Domain;

public sealed class DocumentSummary
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
