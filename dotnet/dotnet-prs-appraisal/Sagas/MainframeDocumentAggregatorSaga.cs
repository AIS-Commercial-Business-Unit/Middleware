using System.Text;
using System.Text.Json;
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
    IHandleMessages<MainframeDocumentChunkReceivedEvent>,
    IHandleTimeouts<Uc4MainframeDocumentAggregatorTimeoutMessage>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            .ToMessage<MainframeDocumentChunkReceivedEvent>(message => message.RequestId)
            .ToMessage<Uc4MainframeDocumentAggregatorTimeoutMessage>(message => message.RequestId);
    }

    public async Task Handle(StartMainframeDocumentAggregationCommand message, IMessageHandlerContext context)
    {
        Data ??= new MainframeDocumentAggregatorSagaData();
        Data.RequestId = message.RequestId;
        Data.DocumentKey = message.DocumentKey;
        Data.StartedAt = message.RequestedAt;

        await RequestTimeout(context, TimeSpan.FromSeconds(30), new Uc4MainframeDocumentAggregatorTimeoutMessage
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

    public async Task Handle(MainframeDocumentChunkReceivedEvent message, IMessageHandlerContext context)
    {
        Data ??= new MainframeDocumentAggregatorSagaData();
        var accumulatedChunks = GetAccumulatedChunks();
        accumulatedChunks.Add(message.ChunkPayload);
        Data.AccumulatedChunksJson = JsonSerializer.Serialize(accumulatedChunks, JsonOptions);

        if (!message.IsFinal)
        {
            return;
        }

        Data.IsFinalChunkReceived = true;
        await PublishCompletionAsync(context, accumulatedChunks, timedOut: false).ConfigureAwait(false);
    }

    public Task Timeout(Uc4MainframeDocumentAggregatorTimeoutMessage state, IMessageHandlerContext context)
        => PublishCompletionAsync(context, [], timedOut: true);

    private async Task PublishCompletionAsync(IMessageHandlerContext context, List<string> accumulatedChunks, bool timedOut)
    {
        var payload = timedOut ? string.Empty : string.Concat(accumulatedChunks);

        LogEdaFlow(Data.RequestId, "MainframeDocumentComplete", "MainframeDocumentAggregator", "DocumentRetrievalSaga", "nsb.uc4appraisaldocumentretrieved", "published");
        await context.Publish(new Uc4AppraisalDocumentRetrievedEvent
        {
            RequestId = Data.RequestId,
            DocumentKey = Data.DocumentKey,
            SourceSystem = "Mainframe",
            ContentType = timedOut ? string.Empty : "application/pdf",
            ContentBase64 = timedOut ? string.Empty : Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)),
            FileName = timedOut ? string.Empty : $"appraisal-{Data.DocumentKey}.pdf"
        }).ConfigureAwait(false);

        MarkAsComplete();
    }

    private List<string> GetAccumulatedChunks() => string.IsNullOrWhiteSpace(Data.AccumulatedChunksJson)
        ? []
        : JsonSerializer.Deserialize<List<string>>(Data.AccumulatedChunksJson, JsonOptions) ?? [];

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
