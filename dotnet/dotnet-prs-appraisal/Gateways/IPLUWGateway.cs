namespace dotnet_prs_appraisal.Gateways;

// ⚠️ DEMO GAP: PLUW (@Work) gateway — stubbed for demo
// Production: WCF-WSHTTP call to PLUW service
// REPLACE_ME_PLUW_API_ENDPOINT and REPLACE_ME_PLUW_WCF_SCHEMA
public interface IPLUWGateway
{
    /// <summary>
    /// Create an appraisal or inspection work item in PLUW @Work.
    /// ⚠️ DEMO GAP: PLUW API request/response schema unknown — REPLACE_ME_PLUW_CREATE_SCHEMA
    /// </summary>
    Task<PLUWCreateResult> CreateAppraisalWorkItemAsync(
        string appraisalId,
        string policyNumber,
        string inspectionTypeCode,
        string producerCode,
        CancellationToken ct = default);

    /// <summary>
    /// Close an existing PLUW work item (used by StatusCode=15 workflow).
    /// ⚠️ DEMO GAP: PLUW close API schema unknown — REPLACE_ME_PLUW_CLOSE_SCHEMA
    /// </summary>
    Task CloseAppraisalAsync(string pluwWorkItemId, string appraisalId, CancellationToken ct = default);

    /// <summary>
    /// Update @Work inspection status via PLUW gateway.
    /// ⚠️ DEMO GAP: @Work MQ message format unknown — REPLACE_ME_ATWORK_MQ_FORMAT
    /// </summary>
    Task UpdateAtWorkAsync(string appraisalId, string inspectionId, int statusCode, CancellationToken ct = default);
}

public sealed class PLUWCreateResult
{
    // ⚠️ DEMO GAP: PLUW work item ID format unknown — REPLACE_ME_PLUW_WORK_ITEM_ID_FORMAT
    public string PLUWWorkItemId { get; set; } = string.Empty;

    // ⚠️ DEMO GAP: @Work inspection ID format unknown — REPLACE_ME_ATWORK_ID_FORMAT
    public string AtWorkInspectionId { get; set; } = string.Empty;

    public bool Success { get; set; }
}
