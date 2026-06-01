using dotnet_prs_appraisal.Domain;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;

namespace dotnet_prs_appraisal.Handlers;

public sealed class AtWorkDocumentRetrievalHandler : IHandleMessages<AppraisalDocumentRetrievalRequestedEvent>
{
    private readonly ILogger<AtWorkDocumentRetrievalHandler> _logger;

    public AtWorkDocumentRetrievalHandler(ILogger<AtWorkDocumentRetrievalHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(AppraisalDocumentRetrievalRequestedEvent message, IMessageHandlerContext context)
    {
        LogEdaFlow(message.RequestId, "AtWorkDocumentRetrieve", "AtWorkDocumentRetrievalHandler", "AtWork", "atwork.retrieve.document", "published");
        var result = AtWorkFixture.BuildRetrievalResult(message.RequestId, message.DocumentKey);
        LogEdaFlow(message.RequestId, "AtWorkDocumentRetrieved", "AtWork", "AtWorkDocumentRetrievalHandler", "atwork.retrieve.document", "consumed");

        await context.Publish(new AtWorkDocumentRetrievedEvent
        {
            RequestId = message.RequestId,
            PolicyNumber = message.PolicyNumber,
            DocumentKey = message.DocumentKey,
            Content = result.ContentBase64,
            MimeType = result.ContentType
        }).ConfigureAwait(false);
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
