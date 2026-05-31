using dotnet_prs_appraisal.Domain;
using Middleware.Contracts.Events;
using Middleware.Contracts.Models;
using NServiceBus;
using Serilog.Context;

namespace dotnet_prs_appraisal.Handlers;

public sealed class AtWorkDocumentListHandler : IHandleMessages<Uc4AppraisalDocumentListRequestedEvent>
{
    private readonly ILogger<AtWorkDocumentListHandler> _logger;

    public AtWorkDocumentListHandler(ILogger<AtWorkDocumentListHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(Uc4AppraisalDocumentListRequestedEvent message, IMessageHandlerContext context)
    {
        LogEdaFlow(message.RequestId, "AppraisalDocumentListRequested", "DocumentListSaga", "AtWorkHandler", "nsb.uc4appraisaldocumentlistrequested", "consumed");

        LogEdaFlow(message.RequestId, "AtWorkListQuery", "AtWorkHandler", "AtWork", "atwork.query.list", "published");
        var documents = AtWorkFixture.GetDocuments(message.PolicyNumber);
        LogEdaFlow(message.RequestId, "AtWorkListResponse", "AtWork", "AtWorkHandler", "atwork.query.list", "consumed");

        await context.Publish(new Uc4AtWorkDocumentListCompletedEvent
        {
            RequestId = message.RequestId,
            Documents = documents.Select(d => new Uc4DocumentSummary
            {
                DocumentId = d.DocumentId,
                DocumentKey = d.DocumentKey,
                SourceSystem = d.SourceSystem,
                DocumentType = d.DocumentType,
                DocumentName = d.DocumentName,
                DocumentDate = d.DocumentDate,
                PolicyNumber = d.PolicyNumber,
                Status = d.Status
            }).ToList()
        }).ConfigureAwait(false);

        LogEdaFlow(message.RequestId, "AtWorkDocumentListCompleted", "AtWorkHandler", "DocumentListSaga", "nsb.uc4atworkdocumentlistcompleted", "published");
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
