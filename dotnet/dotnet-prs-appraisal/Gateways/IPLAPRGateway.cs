namespace dotnet_prs_appraisal.Gateways;

// ⚠️ DEMO GAP: PLAPR database gateway — stubbed for demo
// Production: SQL stored procedure adapter (WCF-SQL adapter in BizTalk)
// REPLACE_ME_PLAPR_DB_CONNECTION and REPLACE_ME_PLAPR_STORED_PROC_NAMES
public interface IPLAPRGateway
{
    /// <summary>
    /// Update the PLAPR appraisal staging record.
    /// ⚠️ DEMO GAP: PLAPR table schema and stored procedure name unknown
    /// REPLACE_ME_PLAPR_UPDATE_PROC — ask PRS team for PLAPR schema
    /// </summary>
    Task UpdateAppraisalRecordAsync(
        string appraisalId,
        string policyNumber,
        int statusCode,
        CancellationToken ct = default);

    /// <summary>
    /// Insert a new PLAPR staging record.
    /// ⚠️ DEMO GAP: PLAPR insert schema unknown — REPLACE_ME_PLAPR_INSERT_SCHEMA
    /// </summary>
    Task InsertAppraisalRecordAsync(
        string appraisalId,
        string policyNumber,
        string inspectionTypeCode,
        CancellationToken ct = default);
}
