using dotnet_prs_appraisal.Infrastructure;
using Middleware.Contracts.Events;
using Middleware.Contracts.Models;
using NServiceBus;
using NServiceBus.Persistence.Sql;
using Serilog.Context;

namespace dotnet_prs_appraisal.Sagas;

[SqlSaga(tableSuffix: "MfListAggregator")]
public sealed class MainframeListAggregatorSaga :
    Saga<MainframeListAggregatorSagaData>,
    IAmStartedByMessages<AppraisalDocumentListRequestedEvent>,
    IHandleMessages<MainframeListAccumulationCompleteEvent>,
    IHandleTimeouts<MainframeListAggregatorTimeoutMessage>
{
    private readonly IAccumulatorRepository _accumulatorRepository;
    private readonly IArtemisAdapter _artemisAdapter;
    private readonly ILogger<MainframeListAggregatorSaga> _logger;

    public MainframeListAggregatorSaga(
        IAccumulatorRepository accumulatorRepository,
        IArtemisAdapter artemisAdapter,
        ILogger<MainframeListAggregatorSaga> logger)
    {
        _accumulatorRepository = accumulatorRepository;
        _artemisAdapter = artemisAdapter;
        _logger = logger;
    }

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MainframeListAggregatorSagaData> mapper)
    {
        mapper.MapSaga(sagaData => sagaData.RequestId)
            .ToMessage<AppraisalDocumentListRequestedEvent>(message => message.RequestId)
            .ToMessage<MainframeListAccumulationCompleteEvent>(message => message.RequestId)
            .ToMessage<MainframeListAggregatorTimeoutMessage>(message => message.RequestId);
    }

    public async Task Handle(AppraisalDocumentListRequestedEvent message, IMessageHandlerContext context)
    {
        var isRetry = Data?.RequestId == message.RequestId;
        Data ??= new MainframeListAggregatorSagaData();
        Data.RequestId = message.RequestId;
        Data.PolicyNumber = message.PolicyNumber;
        Data.StartedAt = message.RequestedAt;

        if (isRetry)
        {
            _logger.LogWarning("Duplicate AppraisalDocumentListRequestedEvent for {RequestId} — skipping MQ send.", message.RequestId);
            return;
        }

        await RequestTimeout(context, TimeSpan.FromSeconds(18), new MainframeListAggregatorTimeoutMessage
        {
            RequestId = message.RequestId
        }).ConfigureAwait(false);

        try
        {
            LogEdaFlow(message.RequestId, "MqListRequest", "MainframeListAggregator", "Mainframe", "APPRAISAL.LIST.REQUEST", "published");
            _artemisAdapter.SendListRequest(message.RequestId, message.PolicyNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Artemis list request for {RequestId}. Timeout will publish partial or empty results.", message.RequestId);
        }
    }

    public Task Handle(MainframeListAccumulationCompleteEvent message, IMessageHandlerContext context)
        => PublishCompletionAsync(context, message.RequestId, message.Documents);

    public async Task Timeout(MainframeListAggregatorTimeoutMessage state, IMessageHandlerContext context)
    {
        var requestId = string.IsNullOrWhiteSpace(Data?.RequestId) ? state.RequestId : Data.RequestId;
        var documents = await _accumulatorRepository
            .GetListDocumentsAsync(requestId, context.CancellationToken)
            .ConfigureAwait(false);

        await PublishCompletionAsync(context, requestId, documents).ConfigureAwait(false);
    }

    private async Task PublishCompletionAsync(
        IMessageHandlerContext context,
        string requestId,
        IReadOnlyCollection<AppraisalDocumentSummary> documents)
    {
        await context.Publish(new MainframeDocumentListCompletedEvent
        {
            RequestId = requestId,
            Documents = documents.ToList()
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
