namespace dotnet_prs_appraisal.Infrastructure;

public interface IDocumentRetrievalRequestRepository
{
    Task CreateAsync(string requestId, string documentKey, string sourceSystem);
    Task<DocumentRetrievalRequestRecord?> FindAsync(string requestId);
    Task CompleteAsync(string requestId, string contentType, string contentBase64, string fileName, string sourceSystem);
}
