namespace dotnet_prs_appraisal.Infrastructure;

public sealed class DocumentRetrievalRequestRecord
{
    public string RequestId { get; set; } = string.Empty;
    public string DocumentKey { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    /// <summary>Pending | Complete | TimedOut</summary>
    public string Status { get; set; } = "Pending";
    public string ContentType { get; set; } = string.Empty;
    public string ContentBase64 { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
