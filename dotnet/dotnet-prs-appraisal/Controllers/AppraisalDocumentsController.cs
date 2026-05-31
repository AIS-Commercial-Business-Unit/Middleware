using dotnet_prs_appraisal.Domain;
using dotnet_prs_appraisal.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Middleware.Contracts.Commands;
using NServiceBus;
using Serilog.Context;

namespace dotnet_prs_appraisal.Controllers;

[ApiController]
public sealed class AppraisalDocumentsController : ControllerBase
{
    private readonly ICallbackRegistry _callbackRegistry;
    private readonly ILogger<AppraisalDocumentsController> _logger;
    private readonly IMessageSession _messageSession;

    public AppraisalDocumentsController(
        IMessageSession messageSession,
        ICallbackRegistry callbackRegistry,
        ILogger<AppraisalDocumentsController> logger)
    {
        _messageSession = messageSession;
        _callbackRegistry = callbackRegistry;
        _logger = logger;
    }

    [HttpGet("/api/policies/{policyNumber}/appraisals/documents")]
    public async Task<IActionResult> GetDocumentList(string policyNumber, [FromQuery] int timeoutSeconds = 30)
    {
        var requestId = Guid.NewGuid().ToString("N");
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = requestId });

        _logger.LogInformation("Entering GetDocumentList for {PolicyNumber}.", policyNumber);

        LogEdaFlow(requestId, "GetAppraisalDocumentListRequest", "API", "PrsAppraisal", "http.request.list", "consumed");

        var tcs = new TaskCompletionSource<DocumentListResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _callbackRegistry.Register(requestId, tcs);

        await _messageSession.Send(new GetAppraisalDocumentListCommand
        {
            RequestId = requestId,
            PolicyNumber = policyNumber,
            CorrelationId = requestId,
            RequestedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);

        var result = await AwaitResultAsync(tcs.Task, NormalizeTimeout(timeoutSeconds)).ConfigureAwait(false);
        if (result is null)
        {
            LogEdaFlow(requestId, "GetAppraisalDocumentListTimeout", "PrsAppraisal", "API", "http.response.list", "published");
            _logger.LogInformation("Exiting GetDocumentList for {PolicyNumber} with Accepted.", policyNumber);
            return Accepted(new
            {
                requestId,
                status = "Processing",
                statusUrl = $"/api/appraisal-document-requests/{requestId}"
            });
        }

        LogEdaFlow(requestId, "GetAppraisalDocumentListResponse", "PrsAppraisal", "API", "http.response.list", "published");
        _logger.LogInformation("Exiting GetDocumentList for {PolicyNumber} with {DocumentCount} documents.", policyNumber, result.Documents.Count);
        return Ok(new
        {
            requestId = result.RequestId,
            policyNumber = result.PolicyNumber,
            documents = result.Documents,
            partialResult = result.PartialResult
        });
    }

    [HttpGet("/api/appraisals/documents/{documentKey}")]
    public async Task<IActionResult> GetDocument(string documentKey, [FromQuery] string sourceSystem = "Mainframe")
    {
        var requestId = Guid.NewGuid().ToString("N");
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = requestId });

        _logger.LogInformation("Entering GetDocument for {DocumentKey} from {SourceSystem}.", documentKey, sourceSystem);

        LogEdaFlow(requestId, "RetrieveAppraisalDocumentRequest", "API", "PrsAppraisal", "http.request.document", "consumed");

        var tcs = new TaskCompletionSource<DocumentRetrievalResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _callbackRegistry.Register(requestId, tcs);

        await _messageSession.Send(new RetrieveAppraisalDocumentCommand
        {
            RequestId = requestId,
            DocumentKey = documentKey,
            SourceSystem = sourceSystem,
            CorrelationId = requestId,
            RequestedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);

        var result = await AwaitResultAsync(tcs.Task, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        if (result is null || string.IsNullOrWhiteSpace(result.ContentBase64))
        {
            LogEdaFlow(requestId, "RetrieveAppraisalDocumentTimeout", "PrsAppraisal", "API", "http.response.document", "published");
            _logger.LogInformation("Exiting GetDocument for {DocumentKey} with Accepted.", documentKey);
            return Accepted(new
            {
                requestId,
                status = "Processing",
                statusUrl = $"/api/appraisal-document-requests/{requestId}"
            });
        }

        LogEdaFlow(requestId, "RetrieveAppraisalDocumentResponse", "PrsAppraisal", "API", "http.response.document", "published");
        _logger.LogInformation("Exiting GetDocument for {DocumentKey} with Success.", documentKey);
        return Ok(new
        {
            requestId = result.RequestId,
            documentId = result.DocumentId,
            documentKey = result.DocumentKey,
            sourceSystem = result.SourceSystem,
            contentType = result.ContentType,
            contentBase64 = result.ContentBase64,
            fileName = result.FileName
        });
    }

    private void LogEdaFlow(string requestId, string messageType, string from, string to, string topic, string direction = "consumed")
    {
        using var _1 = LogContext.PushProperty("EDA_Event", "EDA_FLOW");
        using var _2 = LogContext.PushProperty("EDA_IssuanceId", requestId);
        using var _3 = LogContext.PushProperty("EDA_MessageType", messageType);
        using var _4 = LogContext.PushProperty("EDA_From", from);
        using var _5 = LogContext.PushProperty("EDA_To", to);
        using var _6 = LogContext.PushProperty("EDA_Direction", direction);
        using var _7 = LogContext.PushProperty("EDA_Stack", "dotnet");
        using var _8 = LogContext.PushProperty("EDA_Topic", topic);
        _logger.LogInformation("EDA_FLOW {EDA_MessageType} {EDA_From} -> {EDA_To}", messageType, from, to);
    }

    private static TimeSpan NormalizeTimeout(int timeoutSeconds)
    {
        var boundedSeconds = timeoutSeconds <= 0 ? 30 : Math.Min(timeoutSeconds, 30);
        return TimeSpan.FromSeconds(boundedSeconds);
    }

    private static async Task<T?> AwaitResultAsync<T>(Task<T> task, TimeSpan timeout)
        where T : class
    {
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            return await task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
