using System.Text.Json;
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
    IAmStartedByMessages<Uc4AppraisalDocumentListRequestedEvent>,
    IHandleMessages<MainframeAppraisalListPartReceivedEvent>,
    IHandleTimeouts<Uc4MainframeListAggregatorTimeoutMessage>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IArtemisAdapter _artemisAdapter;
    private readonly ILogger<MainframeListAggregatorSaga> _logger;

    public MainframeListAggregatorSaga(
        IArtemisAdapter artemisAdapter,
        ILogger<MainframeListAggregatorSaga> logger)
    {
        _artemisAdapter = artemisAdapter;
        _logger = logger;
    }

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MainframeListAggregatorSagaData> mapper)
    {
        mapper.MapSaga(sagaData => sagaData.RequestId)
            .ToMessage<Uc4AppraisalDocumentListRequestedEvent>(message => message.RequestId)
            .ToMessage<MainframeAppraisalListPartReceivedEvent>(message => message.RequestId)
            .ToMessage<Uc4MainframeListAggregatorTimeoutMessage>(message => message.RequestId);
    }

    public async Task Handle(Uc4AppraisalDocumentListRequestedEvent message, IMessageHandlerContext context)
    {
        Data ??= new MainframeListAggregatorSagaData();
        Data.RequestId = message.RequestId;
        Data.PolicyNumber = message.PolicyNumber;
        Data.StartedAt = message.RequestedAt;

        await RequestTimeout(context, TimeSpan.FromSeconds(30), new Uc4MainframeListAggregatorTimeoutMessage
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

    public async Task Handle(MainframeAppraisalListPartReceivedEvent message, IMessageHandlerContext context)
    {
        Data ??= new MainframeListAggregatorSagaData();
        Data.ExpectedTotal = Math.Max(Data.ExpectedTotal, message.TotalExpected);

        var accumulatedDocuments = GetAccumulatedDocuments();
        var existing = accumulatedDocuments.FirstOrDefault(item => item.SequenceNumber == message.SequenceNumber);
        if (existing is null)
        {
            accumulatedDocuments.Add(new AccumulatedDocument
            {
                SequenceNumber = message.SequenceNumber,
                Document = message.Document
            });
        }
        else
        {
            existing.Document = message.Document;
        }

        accumulatedDocuments = accumulatedDocuments
            .OrderBy(item => item.SequenceNumber)
            .ToList();

        Data.ReceivedCount = accumulatedDocuments.Count;
        Data.AccumulatedDocumentsJson = JsonSerializer.Serialize(accumulatedDocuments, JsonOptions);

        if (Data.ExpectedTotal > 0 && Data.ReceivedCount >= Data.ExpectedTotal)
        {
            await PublishCompletionAsync(context, accumulatedDocuments).ConfigureAwait(false);
        }
    }

    public Task Timeout(Uc4MainframeListAggregatorTimeoutMessage state, IMessageHandlerContext context)
        => PublishCompletionAsync(context, GetAccumulatedDocuments());

    private async Task PublishCompletionAsync(IMessageHandlerContext context, List<AccumulatedDocument> accumulatedDocuments)
    {
        var orderedDocuments = accumulatedDocuments
            .OrderBy(item => item.SequenceNumber)
            .Select(item => item.Document)
            .ToList();

        LogEdaFlow(Data.RequestId, "MainframeListComplete", "MainframeListAggregator", "DocumentListSaga", "nsb.uc4mainframedocumentlistcompleted", "published");
        await context.Publish(new Uc4MainframeDocumentListCompletedEvent
        {
            RequestId = Data.RequestId,
            Documents = orderedDocuments
        }).ConfigureAwait(false);

        MarkAsComplete();
    }

    private List<AccumulatedDocument> GetAccumulatedDocuments() => string.IsNullOrWhiteSpace(Data.AccumulatedDocumentsJson)
        ? []
        : JsonSerializer.Deserialize<List<AccumulatedDocument>>(Data.AccumulatedDocumentsJson, JsonOptions) ?? [];

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

    private sealed class AccumulatedDocument
    {
        public int SequenceNumber { get; set; }

        public Uc4DocumentSummary Document { get; set; } = new();
    }
}
