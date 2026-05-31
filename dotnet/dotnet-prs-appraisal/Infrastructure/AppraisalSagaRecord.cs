namespace dotnet_prs_appraisal.Infrastructure;

public sealed class AppraisalSagaRecord
{
    public string AppraisalId { get; set; } = string.Empty;
    public string InspectionId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string InspectionTypeCode { get; set; } = string.Empty;
    public string ProducerCode { get; set; } = string.Empty;
    public string UWControlCode { get; set; } = string.Empty;
    public bool PLUWCreateComplete { get; set; }
    public bool UWDeterminationComplete { get; set; }
    public string PLUWWorkItemId { get; set; } = string.Empty;
    public string UWAssignmentType { get; set; } = string.Empty;
    public int SuspenseDays { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string Status { get; set; } = "Initiated";
    public string? FailureReason { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
