using dotnet_prs_appraisal.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace dotnet_prs_appraisal.Controllers;

[ApiController]
public sealed class FlowSagasController : ControllerBase
{
    private readonly IDocumentListRequestRepository _listRepo;
    private readonly IDocumentRetrievalRequestRepository _retrievalRepo;

    public FlowSagasController(
        IDocumentListRequestRepository listRepo,
        IDocumentRetrievalRequestRepository retrievalRepo)
    {
        _listRepo = listRepo;
        _retrievalRepo = retrievalRepo;
    }

    [HttpGet("/api/appraisals/flow-sagas/{requestId}")]
    public async Task<IActionResult> GetFlowSagas(string requestId)
    {
        var listRecord = await _listRepo.FindAsync(requestId).ConfigureAwait(false);
        _ = _retrievalRepo;

        return Ok(new
        {
            documentListSaga = listRecord is null ? null : new
            {
                requestId = listRecord.RequestId,
                policyNumber = listRecord.PolicyNumber,
                status = listRecord.Status,
                documentCount = listRecord.Documents?.Count ?? 0,
                documents = listRecord.Documents,
                partialResult = listRecord.PartialResult,
                createdAt = listRecord.CreatedAt,
                completedAt = listRecord.CompletedAt,
            }
        });
    }
}
