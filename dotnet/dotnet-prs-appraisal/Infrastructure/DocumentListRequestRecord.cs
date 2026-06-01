using Middleware.Contracts.Models;

namespace dotnet_prs_appraisal.Infrastructure;

public sealed class DocumentListRequestRecord
{
    public string RequestId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    /// <summary>Pending | Complete | TimedOut</summary>
    public string Status { get; set; } = "Pending";
    public List<AppraisalDocumentSummary> Documents { get; set; } = new();
    public bool PartialResult { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
