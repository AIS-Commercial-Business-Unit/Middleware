using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using Middleware.Contracts.Messages;
using NServiceBus;
using Serilog.Context;

namespace dotnet_prs_appraisal.Sagas;

public sealed class DocumentRetrievalSaga :
    Saga<DocumentRetrievalSagaData>,
    IAmStartedByMessages<RetrieveAppraisalDocumentCommand>,
    IHandleMessages<Uc4AtWorkDocumentRetrievedEvent>,
    IHandleMessages<Uc4AppraisalDocumentRetrievedEvent>,
    IHandleTimeouts<Uc4DocumentRetrievalSagaTimeoutMessage>
{
    private readonly ILogger<DocumentRetrievalSaga> _logger;

    public DocumentRetrievalSaga(ILogger<DocumentRetrievalSaga> logger)
    {
        _logger = logger;
    }

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<DocumentRetrievalSagaData> mapper)
    {
        mapper.MapSaga(sagaData => sagaData.RequestId)
            .ToMessage<RetrieveAppraisalDocumentCommand>(message => message.RequestId)
            .ToMessage<Uc4AtWorkDocumentRetrievedEvent>(message => message.RequestId)
            .ToMessage<Uc4AppraisalDocumentRetrievedEvent>(message => message.RequestId);
    }

    public async Task Handle(RetrieveAppraisalDocumentCommand message, IMessageHandlerContext context)
    {
        Data ??= new DocumentRetrievalSagaData();
        Data.RequestId = message.RequestId;
        Data.DocumentKey = message.DocumentKey;
        Data.SourceSystem = message.SourceSystem;
        Data.StartedAt = message.RequestedAt;

        await RequestTimeout(context, TimeSpan.FromSeconds(30), new Uc4DocumentRetrievalSagaTimeoutMessage
        {
            RequestId = message.RequestId
        }).ConfigureAwait(false);

        if (string.Equals(message.SourceSystem, "AtWork", StringComparison.OrdinalIgnoreCase)
            || message.DocumentKey.Contains("_RiskID_", StringComparison.OrdinalIgnoreCase))
        {
            Data.AtWorkPending = true;
            LogEdaFlow(message.RequestId, "AppraisalDocumentRetrievalRequested", "DocumentRetrievalSaga", "AtWorkDocumentRetrievalHandler", "nsb.uc4appraisaldocumentretrievalrequested", "published");
            await context.Publish(new Uc4AppraisalDocumentRetrievalRequestedEvent
            {
                RequestId = message.RequestId,
                PolicyNumber = Data.PolicyNumber,
                DocumentKey = message.DocumentKey
            }).ConfigureAwait(false);
            return;
        }

        await context.Send(new StartMainframeDocumentAggregationCommand
        {
            RequestId = message.RequestId,
            DocumentKey = message.DocumentKey,
            RequestedAt = message.RequestedAt
        }).ConfigureAwait(false);
    }

    public async Task Handle(Uc4AtWorkDocumentRetrievedEvent message, IMessageHandlerContext context)
    {
        LogEdaFlow(message.RequestId, "AtWorkDocumentRetrieved", "AtWorkDocumentRetrievalHandler", "DocumentRetrievalSaga", "nsb.uc4atworkdocumentretrieved", "consumed");
        Data.AtWorkContent = message.Content;
        Data.AtWorkMimeType = message.MimeType;
        Data.AtWorkDone = true;

        await TryCompleteAtWorkAsync(context).ConfigureAwait(false);
    }

    public async Task Handle(Uc4AppraisalDocumentRetrievedEvent message, IMessageHandlerContext context)
    {
        await context.Reply(new RetrieveAppraisalDocumentResponse
        {
            RequestId = Data.RequestId,
            DocumentId = Data.DocumentKey,
            DocumentKey = Data.DocumentKey,
            SourceSystem = string.IsNullOrWhiteSpace(message.SourceSystem) ? Data.SourceSystem : message.SourceSystem,
            ContentType = message.ContentType,
            ContentBase64 = message.ContentBase64,
            FileName = string.IsNullOrWhiteSpace(message.FileName) ? $"appraisal-{Data.DocumentKey}.pdf" : message.FileName
        }).ConfigureAwait(false);

        MarkAsComplete();
    }

    public async Task Timeout(Uc4DocumentRetrievalSagaTimeoutMessage state, IMessageHandlerContext context)
    {
        await context.Reply(new RetrieveAppraisalDocumentResponse
        {
            RequestId = Data.RequestId,
            DocumentId = Data.DocumentKey,
            DocumentKey = Data.DocumentKey,
            SourceSystem = Data.SourceSystem,
            ContentType = string.Empty,
            ContentBase64 = string.Empty,
            FileName = string.Empty
        }).ConfigureAwait(false);

        MarkAsComplete();
    }

    private async Task TryCompleteAtWorkAsync(IMessageHandlerContext context)
    {
        if (Data.AtWorkPending && !Data.AtWorkDone)
            return;

        await context.Reply(new RetrieveAppraisalDocumentResponse
        {
            RequestId = Data.RequestId,
            DocumentId = Data.DocumentKey,
            DocumentKey = Data.DocumentKey,
            SourceSystem = "AtWork",
            ContentType = Data.AtWorkMimeType,
            ContentBase64 = Data.AtWorkContent,
            FileName = $"appraisal-{Data.DocumentKey}.pdf"
        }).ConfigureAwait(false);

        MarkAsComplete();
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
}

