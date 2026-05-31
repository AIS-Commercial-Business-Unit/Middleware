namespace dotnet_prs_appraisal.Domain;

public sealed class DocumentListResult
{
    public string RequestId { get; set; } = string.Empty;

    public string PolicyNumber { get; set; } = string.Empty;

    public List<DocumentSummary> Documents { get; set; } = new();

    public bool PartialResult { get; set; }
}
