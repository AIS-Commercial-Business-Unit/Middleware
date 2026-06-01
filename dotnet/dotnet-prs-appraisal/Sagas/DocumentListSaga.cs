using System.Text.Json;
using dotnet_prs_appraisal.Domain;
using dotnet_prs_appraisal.Infrastructure;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using Middleware.Contracts.Models;
using NServiceBus;

namespace dotnet_prs_appraisal.Sagas;

public sealed class DocumentListSaga :
    Saga<DocumentListSagaData>,
    IAmStartedByMessages<GetAppraisalDocumentListCommand>,
    IHandleMessages<AtWorkDocumentListCompletedEvent>,
    IHandleMessages<MainframeDocumentListCompletedEvent>,
    IHandleTimeouts<DocumentListSagaTimeoutMessage>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDocumentListRequestRepository _requestRepository;

    public DocumentListSaga(IDocumentListRequestRepository requestRepository)
        => _requestRepository = requestRepository;

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<DocumentListSagaData> mapper)
    {
        mapper.MapSaga(sagaData => sagaData.RequestId)
            .ToMessage<GetAppraisalDocumentListCommand>(message => message.RequestId)
            .ToMessage<AtWorkDocumentListCompletedEvent>(message => message.RequestId)
            .ToMessage<MainframeDocumentListCompletedEvent>(message => message.RequestId);
    }

    public async Task Handle(GetAppraisalDocumentListCommand message, IMessageHandlerContext context)
    {
        Data.RequestId = message.RequestId;
        Data.PolicyNumber = message.PolicyNumber;
        Data.StartedAt = message.RequestedAt;

        await RequestTimeout(context, TimeSpan.FromSeconds(25), new DocumentListSagaTimeoutMessage
        {
            RequestId = message.RequestId
        }).ConfigureAwait(false);

        await context.Publish(new AppraisalDocumentListRequestedEvent
        {
            RequestId = message.RequestId,
            PolicyNumber = message.PolicyNumber,
            RequestedAt = message.RequestedAt
        }).ConfigureAwait(false);
    }

    public async Task Handle(AtWorkDocumentListCompletedEvent message, IMessageHandlerContext context)
    {
        Data.AtWorkDocumentsJson = JsonSerializer.Serialize(message.Documents.Select(ToLocal).ToList(), JsonOptions);
        Data.AtWorkDone = true;

        await TryCompleteAsync().ConfigureAwait(false);
    }

    public async Task Handle(MainframeDocumentListCompletedEvent message, IMessageHandlerContext context)
    {
        Data.MainframeDocumentsJson = JsonSerializer.Serialize(message.Documents.Select(ToLocal).ToList(), JsonOptions);
        Data.MainframeDone = true;

        await TryCompleteAsync().ConfigureAwait(false);
    }

    public async Task Timeout(DocumentListSagaTimeoutMessage state, IMessageHandlerContext context)
    {
        var documents = MergeDocuments().Select(ToContract).ToList();
        await _requestRepository.CompleteAsync(Data.RequestId, documents, partialResult: true).ConfigureAwait(false);
        MarkAsComplete();
    }

    private async Task TryCompleteAsync()
    {
        if (!Data.AtWorkDone || !Data.MainframeDone)
            return;

        var documents = MergeDocuments().Select(ToContract).ToList();
        await _requestRepository.CompleteAsync(Data.RequestId, documents, partialResult: false).ConfigureAwait(false);
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

    private static DocumentSummary ToLocal(AppraisalDocumentSummary document) => new()
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

    private static AppraisalDocumentSummary ToContract(DocumentSummary document) => new()
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

