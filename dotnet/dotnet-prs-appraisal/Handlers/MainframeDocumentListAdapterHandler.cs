using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;

namespace dotnet_prs_appraisal.Handlers;

public sealed class MainframeDocumentListAdapterHandler : IHandleMessages<Uc4AppraisalDocumentListRequestedEvent>
{
    private readonly ILogger<MainframeDocumentListAdapterHandler> _logger;

    public MainframeDocumentListAdapterHandler(ILogger<MainframeDocumentListAdapterHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(Uc4AppraisalDocumentListRequestedEvent message, IMessageHandlerContext context)
    {
        LogEdaFlow(message.RequestId, "AppraisalDocumentListRequested", "DocumentListSaga", "MainframeAdapter", "nsb.uc4appraisaldocumentlistrequested", "consumed");

        await context.Send(new StartMainframeListAggregationCommand
        {
            RequestId = message.RequestId,
            PolicyNumber = message.PolicyNumber,
            RequestedAt = message.RequestedAt
        }).ConfigureAwait(false);

        LogEdaFlow(message.RequestId, "StartMainframeListAggregation", "MainframeAdapter", "MainframeListAggregator", "nsb.startmainframelistaggregation", "published");
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
