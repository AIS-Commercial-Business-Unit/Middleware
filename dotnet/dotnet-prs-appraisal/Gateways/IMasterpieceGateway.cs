namespace dotnet_prs_appraisal.Gateways;

// ⚠️ DEMO GAP: Masterpiece Policy System gateway — stubbed for demo
// Production: WCF call to Masterpiece Transaction 90 (PLIPQP90)
// REPLACE_ME_MASTERPIECE_WCF_ENDPOINT and REPLACE_ME_TX90_REQUEST_SCHEMA
public interface IMasterpieceGateway
{
    /// <summary>
    /// Call Masterpiece Transaction 90 (PLIPQP90) to confirm completion.
    /// ⚠️ DEMO GAP: Transaction 90 request/response schema unknown
    /// REPLACE_ME_MASTERPIECE_TX90_SCHEMA — ask PRS team for PLIPQP90 spec
    /// </summary>
    Task<string> CallTransaction90Async(
        string policyNumber,
        string inspectionId,
        string appraisalId,
        CancellationToken ct = default);
}
