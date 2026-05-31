using System.Text.Json;
using dotnet_prs_appraisal.Domain;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using Middleware.Contracts.Messages;
using Middleware.Contracts.Models;
using NServiceBus;
using Serilog.Context;

namespace dotnet_prs_appraisal.Sagas;

public sealed class DocumentListSaga :
    Saga<DocumentListSagaData>,
    IAmStartedByMessages<GetAppraisalDocumentListCommand>,
    IHandleMessages<Uc4AtWorkDocumentListCompletedEvent>,
    IHandleMessages<Uc4MainframeDocumentListCompletedEvent>,
    IHandleTimeouts<Uc4DocumentListSagaTimeoutMessage>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<DocumentListSaga> _logger;

    public DocumentListSaga(ILogger<DocumentListSaga> logger)
    {
        _logger = logger;
    }

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<DocumentListSagaData> mapper)
    {
        mapper.MapSaga(sagaData => sagaData.RequestId)
            .ToMessage<GetAppraisalDocumentListCommand>(message => message.RequestId)
            .ToMessage<Uc4AtWorkDocumentListCompletedEvent>(message => message.RequestId)
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

        await context.Publish(new Uc4AppraisalDocumentListRequestedEvent
        {
            RequestId = message.RequestId,
            PolicyNumber = message.PolicyNumber,
            RequestedAt = message.RequestedAt
        }).ConfigureAwait(false);

        LogEdaFlow(message.RequestId, "AppraisalDocumentListRequested", "DocumentListSaga", "AllSubscribers", "nsb.uc4appraisaldocumentlistrequested", "published");
    }

    public async Task Handle(Uc4AtWorkDocumentListCompletedEvent message, IMessageHandlerContext context)
    {
        Data.AtWorkDocumentsJson = JsonSerializer.Serialize(message.Documents.Select(ToLocal).ToList(), JsonOptions);
        Data.AtWorkDone = true;

        await TryCompleteAsync(context).ConfigureAwait(false);
    }

    public async Task Handle(Uc4MainframeDocumentListCompletedEvent message, IMessageHandlerContext context)
    {
        Data.MainframeDocumentsJson = JsonSerializer.Serialize(message.Documents.Select(ToLocal).ToList(), JsonOptions);
        Data.MainframeDone = true;

        await TryCompleteAsync(context).ConfigureAwait(false);
    }

    public async Task Timeout(Uc4DocumentListSagaTimeoutMessage state, IMessageHandlerContext context)
    {
        await context.Reply(new GetAppraisalDocumentListResponse
        {
            RequestId = Data.RequestId,
            PolicyNumber = Data.PolicyNumber,
            Documents = MergeDocuments().Select(d => new Uc4DocumentSummary
            {
                DocumentId = d.DocumentId,
                DocumentKey = d.DocumentKey,
                SourceSystem = d.SourceSystem,
                DocumentType = d.DocumentType,
                DocumentName = d.DocumentName,
                DocumentDate = d.DocumentDate,
                PolicyNumber = d.PolicyNumber,
                Status = d.Status
            }).ToList(),
            PartialResult = true
        }).ConfigureAwait(false);

        MarkAsComplete();
    }

    private async Task TryCompleteAsync(IMessageHandlerContext context)
    {
        if (!Data.AtWorkDone || !Data.MainframeDone)
            return;

        await context.Reply(new GetAppraisalDocumentListResponse
        {
            RequestId = Data.RequestId,
            PolicyNumber = Data.PolicyNumber,
            Documents = MergeDocuments().Select(d => new Uc4DocumentSummary
            {
                DocumentId = d.DocumentId,
                DocumentKey = d.DocumentKey,
                SourceSystem = d.SourceSystem,
                DocumentType = d.DocumentType,
                DocumentName = d.DocumentName,
                DocumentDate = d.DocumentDate,
                PolicyNumber = d.PolicyNumber,
                Status = d.Status
            }).ToList(),
            PartialResult = false
        }).ConfigureAwait(false);

        MarkAsComplete();
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

