namespace dotnet_prs_appraisal.Gateways;

// ⚠️ DEMO GAP: Customer DB gateway for producer cross-reference — stubbed for demo
// Production: SQL stored procedure or REST API call
// REPLACE_ME_CUSTOMER_DB_CONNECTION and REPLACE_ME_PRODUCER_XREF_PROC
public interface ICustomerDBGateway
{
    /// <summary>
    /// Look up producer cross-reference by policy number.
    /// Returns producer code and UW control code for routing.
    /// ⚠️ DEMO GAP: Customer DB schema for producer cross-reference unknown
    /// REPLACE_ME_CUSTOMER_DB_XREF_SCHEMA — ask PRS team for Customer DB schema
    /// </summary>
    Task<ProducerCrossReference?> LookupProducerAsync(
        string policyNumber,
        CancellationToken ct = default);
}

public sealed class ProducerCrossReference
{
    // ⚠️ DEMO GAP: ProducerCode format from real Customer DB — REPLACE_ME_PRODUCER_CODE_FORMAT
    public string ProducerCode { get; set; } = string.Empty;

    // ⚠️ DEMO GAP: UWControlCode values and routing rules — REPLACE_ME_UW_CONTROL_CODE_VALUES
    // Expected: "UA" or "UST" but actual codes may differ
    public string UWControlCode { get; set; } = string.Empty;

    public string ProducerName { get; set; } = string.Empty;
}
