using Middleware.Contracts.Models;

namespace dotnet_prs_appraisal.Infrastructure;

public interface IDocumentListRequestRepository
{
    Task CreateAsync(string requestId, string policyNumber);
    Task<DocumentListRequestRecord?> FindAsync(string requestId);
    Task CompleteAsync(string requestId, List<AppraisalDocumentSummary> documents, bool partialResult);
}
