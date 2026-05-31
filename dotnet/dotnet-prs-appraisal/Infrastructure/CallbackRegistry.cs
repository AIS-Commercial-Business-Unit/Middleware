using System.Collections.Concurrent;
using dotnet_prs_appraisal.Domain;

namespace dotnet_prs_appraisal.Infrastructure;

public sealed class CallbackRegistry : ICallbackRegistry
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DocumentListResult>> _listCallbacks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DocumentRetrievalResult>> _retrievalCallbacks = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string requestId, TaskCompletionSource<DocumentListResult> tcs)
    {
        if (!_listCallbacks.TryAdd(requestId, tcs))
        {
            throw new InvalidOperationException($"A list callback for request '{requestId}' is already registered.");
        }
    }

    public void Register(string requestId, TaskCompletionSource<DocumentRetrievalResult> retrievalTcs)
    {
        if (!_retrievalCallbacks.TryAdd(requestId, retrievalTcs))
        {
            throw new InvalidOperationException($"A retrieval callback for request '{requestId}' is already registered.");
        }
    }

    public bool TryComplete(string requestId, DocumentListResult result)
    {
        if (_listCallbacks.TryRemove(requestId, out var tcs))
        {
            return tcs.TrySetResult(result);
        }

        return false;
    }

    public bool TryComplete(string requestId, DocumentRetrievalResult result)
    {
        if (_retrievalCallbacks.TryRemove(requestId, out var tcs))
        {
            return tcs.TrySetResult(result);
        }

        return false;
    }
}
