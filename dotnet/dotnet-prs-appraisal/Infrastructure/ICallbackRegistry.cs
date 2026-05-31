using dotnet_prs_appraisal.Domain;

namespace dotnet_prs_appraisal.Infrastructure;

public interface ICallbackRegistry
{
    void Register(string requestId, TaskCompletionSource<DocumentListResult> tcs);

    void Register(string requestId, TaskCompletionSource<DocumentRetrievalResult> retrievalTcs);

    bool TryComplete(string requestId, DocumentListResult result);

    bool TryComplete(string requestId, DocumentRetrievalResult result);
}
