using System.Text.Json;
using dotnet_prs_appraisal.Domain;
using dotnet_prs_appraisal.Infrastructure;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using Middleware.Contracts.Models;
using NServiceBus;
using Serilog.Context;

namespace dotnet_prs_appraisal.Sagas;

public sealed class DocumentListSaga :
    Saga<DocumentListSagaData>,
    IAmStartedByMessages<GetAppraisalDocumentListCommand>,
    IHandleMessages<Uc4MainframeDocumentListCompletedEvent>,
    IHandleTimeouts<Uc4DocumentListSagaTimeoutMessage>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICallbackRegistry _callbackRegistry;
    private readonly ILogger<DocumentListSaga> _logger;

    public DocumentListSaga(
        ICallbackRegistry callbackRegistry,
        ILogger<DocumentListSaga> logger)
    {
        _callbackRegistry = callbackRegistry;
        _logger = logger;
    }

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<DocumentListSagaData> mapper)
    {
        mapper.MapSaga(sagaData => sagaData.RequestId)
            .ToMessage<GetAppraisalDocumentListCommand>(message => message.RequestId)
            .ToMessage<Uc4MainframeDocumentListCompletedEvent>(message => message.RequestId);
    }

    public async Task Handle(GetAppraisalDocumentListCommand message, IMessageHandlerContext context)
    {
        Data.RequestId = message.RequestId;
        Data.PolicyNumber = message.PolicyNumber;
        Data.StartedAt = message.RequestedAt;

        await RequestTimeout(context, TimeSpan.FromSeconds(30), new Uc4DocumentListSagaTimeoutMessage
        {
            RequestId = message.RequestId
        }).ConfigureAwait(false);

        LogEdaFlow(message.RequestId, "AtWorkListQuery", "PrsAppraisal", "AtWork", "atwork.query.list", "published");
        var atWorkDocuments = AtWorkFixture.GetDocuments(message.PolicyNumber);
        Data.AtWorkDocumentsJson = JsonSerializer.Serialize(atWorkDocuments, JsonOptions);
        Data.AtWorkDone = true;
        LogEdaFlow(message.RequestId, "AtWorkListResponse", "AtWork", "PrsAppraisal", "atwork.query.list", "consumed");

        await context.Send(new StartMainframeListAggregationCommand
        {
            RequestId = message.RequestId,
            PolicyNumber = message.PolicyNumber,
            RequestedAt = message.RequestedAt
        }).ConfigureAwait(false);
    }

    public Task Handle(Uc4MainframeDocumentListCompletedEvent message, IMessageHandlerContext context)
    {
        Data.MainframeDocumentsJson = JsonSerializer.Serialize(message.Documents.Select(ToLocal).ToList(), JsonOptions);
        Data.MainframeDone = true;

        var mergedDocuments = MergeDocuments();
        _callbackRegistry.TryComplete(
            Data.RequestId,
            new DocumentListResult
            {
                RequestId = Data.RequestId,
                PolicyNumber = Data.PolicyNumber,
                Documents = mergedDocuments,
                PartialResult = !(Data.AtWorkDone && Data.MainframeDone)
            });

        MarkAsComplete();
        return Task.CompletedTask;
    }

    public Task Timeout(Uc4DocumentListSagaTimeoutMessage state, IMessageHandlerContext context)
    {
        _callbackRegistry.TryComplete(
            Data.RequestId,
            new DocumentListResult
            {
                RequestId = Data.RequestId,
                PolicyNumber = Data.PolicyNumber,
                Documents = MergeDocuments(),
                PartialResult = true
            });

        MarkAsComplete();
        return Task.CompletedTask;
    }

    private List<DocumentSummary> MergeDocuments() => GetAtWorkDocuments()
        .Concat(GetMainframeDocuments())
        .GroupBy(document => document.DocumentKey, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .OrderBy(document => document.DocumentDate)
        .ToList();

    private List<DocumentSummary> GetAtWorkDocuments() => string.IsNullOrWhiteSpace(Data.AtWorkDocumentsJson)
        ? new List<DocumentSummary>()
        : JsonSerializer.Deserialize<List<DocumentSummary>>(Data.AtWorkDocumentsJson, JsonOptions) ?? new List<DocumentSummary>();

    private List<DocumentSummary> GetMainframeDocuments() => string.IsNullOrWhiteSpace(Data.MainframeDocumentsJson)
        ? new List<DocumentSummary>()
        : JsonSerializer.Deserialize<List<DocumentSummary>>(Data.MainframeDocumentsJson, JsonOptions) ?? new List<DocumentSummary>();

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

    private static DocumentSummary ToLocal(Uc4DocumentSummary document) => new()
    {
        DocumentId = document.DocumentId,
        DocumentKey = document.DocumentKey,
        SourceSystem = document.SourceSystem,
        DocumentType = document.DocumentType,
        DocumentName = document.DocumentName,
        DocumentDate = document.DocumentDate,
        PolicyNumber = document.PolicyNumber,
        Status = document.Status
    };
}
