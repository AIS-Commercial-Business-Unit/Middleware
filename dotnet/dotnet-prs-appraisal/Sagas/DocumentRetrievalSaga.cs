using dotnet_prs_appraisal.Infrastructure;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using NServiceBus;

namespace dotnet_prs_appraisal.Sagas;

public sealed class DocumentRetrievalSaga :
    Saga<DocumentRetrievalSagaData>,
    IAmStartedByMessages<RetrieveAppraisalDocumentCommand>,
    IHandleMessages<AtWorkDocumentRetrievedEvent>,
    IHandleMessages<AppraisalDocumentRetrievedEvent>,
    IHandleTimeouts<DocumentRetrievalSagaTimeoutMessage>
{
    private readonly IDocumentRetrievalRequestRepository _requestRepository;

    public DocumentRetrievalSaga(IDocumentRetrievalRequestRepository requestRepository)
        => _requestRepository = requestRepository;

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<DocumentRetrievalSagaData> mapper)
    {
        mapper.MapSaga(sagaData => sagaData.RequestId)
            .ToMessage<RetrieveAppraisalDocumentCommand>(message => message.RequestId)
            .ToMessage<AtWorkDocumentRetrievedEvent>(message => message.RequestId)
            .ToMessage<AppraisalDocumentRetrievedEvent>(message => message.RequestId);
    }

    public async Task Handle(RetrieveAppraisalDocumentCommand message, IMessageHandlerContext context)
    {
        Data ??= new DocumentRetrievalSagaData();
        Data.RequestId = message.RequestId;
        Data.DocumentKey = message.DocumentKey;
        Data.SourceSystem = message.SourceSystem;
        Data.StartedAt = message.RequestedAt;

        await RequestTimeout(context, TimeSpan.FromSeconds(30), new DocumentRetrievalSagaTimeoutMessage
        {
            RequestId = message.RequestId
        }).ConfigureAwait(false);

        if (string.Equals(message.SourceSystem, "AtWork", StringComparison.OrdinalIgnoreCase)
            || message.DocumentKey.Contains("_RiskID_", StringComparison.OrdinalIgnoreCase))
        {
            Data.AtWorkPending = true;
        }

        await context.Publish(new AppraisalDocumentRetrievalRequestedEvent
        {
            RequestId = message.RequestId,
            PolicyNumber = Data.PolicyNumber,
            DocumentKey = message.DocumentKey,
            SourceSystem = message.SourceSystem
        }).ConfigureAwait(false);
    }

    public async Task Handle(AtWorkDocumentRetrievedEvent message, IMessageHandlerContext context)
    {
        Data.AtWorkContent = message.Content;
        Data.AtWorkMimeType = message.MimeType;
        Data.AtWorkDone = true;

        await TryCompleteAtWorkAsync().ConfigureAwait(false);
    }

    public async Task Handle(AppraisalDocumentRetrievedEvent message, IMessageHandlerContext context)
    {
        var sourceSystem = string.IsNullOrWhiteSpace(message.SourceSystem) ? Data.SourceSystem : message.SourceSystem;
        var fileName = string.IsNullOrWhiteSpace(message.FileName) ? $"appraisal-{Data.DocumentKey}.pdf" : message.FileName;

        await _requestRepository.CompleteAsync(
            Data.RequestId,
            message.ContentType,
            message.ContentBase64,
            fileName,
            sourceSystem).ConfigureAwait(false);

        MarkAsComplete();
    }

    public async Task Timeout(DocumentRetrievalSagaTimeoutMessage state, IMessageHandlerContext context)
    {
        await _requestRepository.CompleteAsync(
            Data.RequestId,
            contentType: string.Empty,
            contentBase64: string.Empty,
            fileName: string.Empty,
            sourceSystem: Data.SourceSystem).ConfigureAwait(false);

        MarkAsComplete();
    }

    private async Task TryCompleteAtWorkAsync()
    {
        if (Data.AtWorkPending && !Data.AtWorkDone)
            return;

        await _requestRepository.CompleteAsync(
            Data.RequestId,
            contentType: Data.AtWorkMimeType,
            contentBase64: Data.AtWorkContent,
            fileName: $"appraisal-{Data.DocumentKey}.pdf",
            sourceSystem: "AtWork").ConfigureAwait(false);

        MarkAsComplete();
    }
}
