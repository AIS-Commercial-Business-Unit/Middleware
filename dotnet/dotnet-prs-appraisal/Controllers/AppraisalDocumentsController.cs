using Microsoft.AspNetCore.Mvc;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Messages;
using NServiceBus;
using Serilog.Context;

namespace dotnet_prs_appraisal.Controllers;

[ApiController]
public sealed class AppraisalDocumentsController : ControllerBase
{
    private readonly ILogger<AppraisalDocumentsController> _logger;
    private readonly IMessageSession _messageSession;

    public AppraisalDocumentsController(
        IMessageSession messageSession,
        ILogger<AppraisalDocumentsController> logger)
    {
        _messageSession = messageSession;
        _logger = logger;
    }

    [HttpGet("/api/policies/{policyNumber}/appraisals/documents")]
    public async Task<IActionResult> GetDocumentList(string policyNumber, [FromQuery] int timeoutSeconds = 30, CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N");
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = requestId });

        _logger.LogInformation("Entering GetDocumentList for {PolicyNumber}.", policyNumber);
        LogEdaFlow(requestId, "GetAppraisalDocumentListRequest", "API", "PrsAppraisal", "http.request.list", "consumed");

        var timeout = NormalizeTimeout(timeoutSeconds);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            var options = new SendOptions();
            options.SetDestination("dotnet-prs-appraisal");

            var response = await _messageSession.Request<GetAppraisalDocumentListResponse>(
                new GetAppraisalDocumentListCommand
                {
                    RequestId = requestId,
                    PolicyNumber = policyNumber,
                    CorrelationId = requestId,
                    RequestedAt = DateTimeOffset.UtcNow
                },
                options,
                cts.Token).ConfigureAwait(false);

            LogEdaFlow(requestId, "GetAppraisalDocumentListResponse", "PrsAppraisal", "API", "http.response.list", "published");
            _logger.LogInformation("Exiting GetDocumentList for {PolicyNumber} with {DocumentCount} documents.", policyNumber, response.Documents.Count);

            return Ok(new
            {
                requestId = response.RequestId,
                policyNumber = response.PolicyNumber,
                documents = response.Documents,
                partialResult = response.PartialResult
            });
        }
        catch (OperationCanceledException)
        {
            LogEdaFlow(requestId, "GetAppraisalDocumentListTimeout", "PrsAppraisal", "API", "http.response.list", "published");
            _logger.LogInformation("Exiting GetDocumentList for {PolicyNumber} with Accepted (timeout).", policyNumber);
            return Accepted(new
            {
                requestId,
                status = "Processing",
                statusUrl = $"/api/appraisal-document-requests/{requestId}"
            });
        }
    }

    [HttpGet("/api/appraisals/documents/{documentKey}")]
    public async Task<IActionResult> GetDocument(string documentKey, [FromQuery] string sourceSystem = "Mainframe", CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N");
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = requestId });

        _logger.LogInformation("Entering GetDocument for {DocumentKey} from {SourceSystem}.", documentKey, sourceSystem);
        LogEdaFlow(requestId, "RetrieveAppraisalDocumentRequest", "API", "PrsAppraisal", "http.request.document", "consumed");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            var options = new SendOptions();
            options.SetDestination("dotnet-prs-appraisal");

            var response = await _messageSession.Request<RetrieveAppraisalDocumentResponse>(
                new RetrieveAppraisalDocumentCommand
                {
                    RequestId = requestId,
                    DocumentKey = documentKey,
                    SourceSystem = sourceSystem,
                    CorrelationId = requestId,
                    RequestedAt = DateTimeOffset.UtcNow
                },
                options,
                cts.Token).ConfigureAwait(false);

            LogEdaFlow(requestId, "RetrieveAppraisalDocumentResponse", "PrsAppraisal", "API", "http.response.document", "published");
            _logger.LogInformation("Exiting GetDocument for {DocumentKey} with Success.", documentKey);

            return Ok(new
            {
                requestId = response.RequestId,
                documentId = response.DocumentId,
                documentKey = response.DocumentKey,
                sourceSystem = response.SourceSystem,
                contentType = response.ContentType,
                contentBase64 = response.ContentBase64,
                fileName = response.FileName
            });
        }
        catch (OperationCanceledException)
        {
            LogEdaFlow(requestId, "RetrieveAppraisalDocumentTimeout", "PrsAppraisal", "API", "http.response.document", "published");
            _logger.LogInformation("Exiting GetDocument for {DocumentKey} with Accepted (timeout).", documentKey);
            return Accepted(new
            {
                requestId,
                status = "Processing",
                statusUrl = $"/api/appraisal-document-requests/{requestId}"
            });
        }
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
}

