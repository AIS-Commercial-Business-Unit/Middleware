using Microsoft.AspNetCore.Mvc;
using Middleware.Contracts.Commands;
using NServiceBus;
using Serilog.Context;
using dotnet_prs_appraisal.Infrastructure;

namespace dotnet_prs_appraisal.Controllers;

[ApiController]
public sealed class AppraisalDocumentsController : ControllerBase
{
    private readonly ILogger<AppraisalDocumentsController> _logger;
    private readonly IMessageSession _messageSession;
    private readonly IDocumentListRequestRepository _documentListRepository;
    private readonly IDocumentRetrievalRequestRepository _documentRetrievalRepository;

    public AppraisalDocumentsController(
        IMessageSession messageSession,
        IDocumentListRequestRepository documentListRepository,
        IDocumentRetrievalRequestRepository documentRetrievalRepository,
        ILogger<AppraisalDocumentsController> logger)
    {
        _messageSession = messageSession;
        _documentListRepository = documentListRepository;
        _documentRetrievalRepository = documentRetrievalRepository;
        _logger = logger;
    }

    [HttpGet("/api/policies/{policyNumber}/appraisals/documents")]
    public async Task<IActionResult> GetDocumentList(string policyNumber, [FromQuery] int timeoutSeconds = 30, CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N");
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = requestId });

        _logger.LogInformation("Entering GetDocumentList for {PolicyNumber}.", policyNumber);
        LogEdaFlow(requestId, "HTTP GET /api/documents", "user", "AppraisalDocumentsController", "http.request.list", "consumed");

        await _documentListRepository.CreateAsync(requestId, policyNumber).ConfigureAwait(false);

        var options = new SendOptions();
        options.SetDestination("dotnet-prs-appraisal");
        await _messageSession.Send(new GetAppraisalDocumentListCommand
        {
            RequestId = requestId,
            PolicyNumber = policyNumber,
            CorrelationId = requestId,
            RequestedAt = DateTimeOffset.UtcNow
        }, options).ConfigureAwait(false);

        var timeout = NormalizeTimeout(timeoutSeconds);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            while (true)
            {
                await Task.Delay(1000, cts.Token).ConfigureAwait(false);
                var record = await _documentListRepository.FindAsync(requestId).ConfigureAwait(false);
                if (record?.Status is "Complete" or "TimedOut")
                {
                    _logger.LogInformation(
                        "Exiting GetDocumentList for {PolicyNumber} with {DocumentCount} documents (partial={Partial}).",
                        policyNumber, record.Documents.Count, record.PartialResult);
                    return Ok(new
                    {
                        requestId = record.RequestId,
                        policyNumber = record.PolicyNumber,
                        documents = record.Documents,
                        partialResult = record.PartialResult
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
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
        LogEdaFlow(requestId, "HTTP GET /api/appraisals/documents", "user", "AppraisalDocumentsController", "http.request.document", "consumed");

        await _documentRetrievalRepository.CreateAsync(requestId, documentKey, sourceSystem).ConfigureAwait(false);

        var options = new SendOptions();
        options.SetDestination("dotnet-prs-appraisal");
        await _messageSession.Send(new RetrieveAppraisalDocumentCommand
        {
            RequestId = requestId,
            DocumentKey = documentKey,
            SourceSystem = sourceSystem,
            CorrelationId = requestId,
            RequestedAt = DateTimeOffset.UtcNow
        }, options).ConfigureAwait(false);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            while (true)
            {
                await Task.Delay(1000, cts.Token).ConfigureAwait(false);
                var record = await _documentRetrievalRepository.FindAsync(requestId).ConfigureAwait(false);
                if (record?.Status is "Complete" or "TimedOut")
                {
                    _logger.LogInformation("Exiting GetDocument for {DocumentKey} with {Status}.", documentKey, record.Status);
                    return Ok(new
                    {
                        requestId = record.RequestId,
                        documentId = record.DocumentKey,
                        documentKey = record.DocumentKey,
                        sourceSystem = record.SourceSystem,
                        contentType = record.ContentType,
                        contentBase64 = record.ContentBase64,
                        fileName = record.FileName
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
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
        var boundedSeconds = timeoutSeconds <= 0 ? 30 : Math.Min(timeoutSeconds, 60);
        return TimeSpan.FromSeconds(boundedSeconds);
    }
}

