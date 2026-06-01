using dotnet_prs_appraisal.Infrastructure;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using NServiceBus;
using NServiceBus.Persistence.Sql;
using Serilog.Context;

namespace dotnet_prs_appraisal.Sagas;

[SqlSaga(tableSuffix: "MfDocumentAggregator")]
public sealed class MainframeDocumentAggregatorSaga :
    Saga<MainframeDocumentAggregatorSagaData>,
    IAmStartedByMessages<StartMainframeDocumentAggregationCommand>,
    IHandleMessages<MainframeDocumentAccumulationCompleteEvent>,
    IHandleTimeouts<MainframeDocumentAggregatorTimeoutMessage>
{
    private readonly IArtemisAdapter _artemisAdapter;
    private readonly ILogger<MainframeDocumentAggregatorSaga> _logger;

    public MainframeDocumentAggregatorSaga(
        IArtemisAdapter artemisAdapter,
        ILogger<MainframeDocumentAggregatorSaga> logger)
    {
        _artemisAdapter = artemisAdapter;
        _logger = logger;
    }

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MainframeDocumentAggregatorSagaData> mapper)
    {
        mapper.MapSaga(sagaData => sagaData.RequestId)
            .ToMessage<StartMainframeDocumentAggregationCommand>(message => message.RequestId)
            .ToMessage<MainframeDocumentAccumulationCompleteEvent>(message => message.RequestId)
            .ToMessage<MainframeDocumentAggregatorTimeoutMessage>(message => message.RequestId);
    }

    public async Task Handle(StartMainframeDocumentAggregationCommand message, IMessageHandlerContext context)
    {
        Data.DocumentKey = message.DocumentKey;
        Data.StartedAt = message.RequestedAt;

        if (Data.MqSendInitiated)
        {
            _logger.LogWarning("Duplicate StartMainframeDocumentAggregationCommand for {RequestId} — skipping MQ send.", message.RequestId);
            return;
        }

        Data.MqSendInitiated = true;

        await RequestTimeout(context, TimeSpan.FromSeconds(18), new MainframeDocumentAggregatorTimeoutMessage
        {
            RequestId = message.RequestId
        }).ConfigureAwait(false);

        try
        {
            LogEdaFlow(message.RequestId, "MqDocumentRequest", "MainframeDocumentAggregator", "Mainframe", "APPRAISAL.DOCUMENT.REQUEST", "published");
            _artemisAdapter.SendDocumentRequest(message.RequestId, message.DocumentKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Artemis document request for {RequestId}. Timeout will publish an empty response.", message.RequestId);
        }
    }

    public async Task Handle(MainframeDocumentAccumulationCompleteEvent message, IMessageHandlerContext context)
    {
        var documentKey = string.IsNullOrWhiteSpace(message.DocumentKey) ? Data.DocumentKey : message.DocumentKey;

        await context.Publish(new AppraisalDocumentRetrievedEvent
        {
            RequestId = message.RequestId,
            DocumentKey = documentKey,
            SourceSystem = "Mainframe",
            ContentType = "application/pdf",
            ContentBase64 = message.ContentBase64,
            FileName = $"appraisal-{documentKey}.pdf"
        }).ConfigureAwait(false);

        MarkAsComplete();
    }

    public async Task Timeout(MainframeDocumentAggregatorTimeoutMessage state, IMessageHandlerContext context)
    {
        await context.Publish(new AppraisalDocumentRetrievedEvent
        {
            RequestId = string.IsNullOrWhiteSpace(Data?.RequestId) ? state.RequestId : Data.RequestId,
            DocumentKey = Data?.DocumentKey ?? string.Empty,
            SourceSystem = "Mainframe",
            ContentType = string.Empty,
            ContentBase64 = string.Empty,
            FileName = string.Empty
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
