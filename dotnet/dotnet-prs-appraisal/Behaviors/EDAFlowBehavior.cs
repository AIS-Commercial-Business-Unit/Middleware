using Microsoft.Extensions.Logging;
using NServiceBus.Pipeline;
using Serilog.Context;

namespace dotnet_prs_appraisal.Behaviors;

/// <summary>
/// EDA_FLOW behaviors for dotnet-prs-appraisal.
/// Emit structured log entries consumed by Loki and rendered as sequence diagrams.
/// Uses "appraisalId" as the correlation key (not "issuanceId").
/// </summary>
file static class AppraisalParticipantMap
{
    public const string CurrentEndpoint = "dotnet-prs-appraisal";

    private static readonly Dictionary<string, string> EndpointToParticipant = new(StringComparer.OrdinalIgnoreCase)
    {
        [CurrentEndpoint] = "PrsAppraisal",
        ["dotnet-customer-identity"] = "CustomerIdentity",
        ["dotnet-platform-integration"] = "Integration",
        ["dotnet-platform-notification"] = "Notification",
        ["AtWork"] = "AtWork",
        ["Mainframe"] = "Mainframe",
        ["deipde07-mq-simulator"] = "Mainframe",
        ["mainframelistaggregator"] = "MainframeListAggregator",
        ["mainframedocumentaggregator"] = "MainframeDocumentAggregator",
        ["atworkhandler"] = "AtWorkHandler",
        ["atworkdocumentretrievalhandler"] = "AtWork Retrieval",
    };

    private static readonly Dictionary<string, string> MessageTypeToPrimarySubscriber = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RiskIDStatusUpdateReceivedEvent"] = "PrsAppraisal",
        ["ProducerLookupRequestedEvent"] = "CustomerIdentity",
        ["PLUWAppraisalCreateRequestedEvent"] = "PrsAppraisal",
        ["UWDeterminationRequestedEvent"] = "PrsAppraisal",
        ["AppraisalUnderwriterAssignedEvent"] = "broadcast",
        ["AppraisalCompletedEvent"] = "broadcast",
        ["AppraisalStatusUpdateFailedEvent"] = "broadcast",
        ["GetAppraisalDocumentListCommand"] = "DocumentListSaga",
        ["RetrieveAppraisalDocumentCommand"] = "DocumentRetrievalSaga",
        ["Uc4AppraisalDocumentListRequestedEvent"] = "broadcast",
        ["Uc4AppraisalDocumentRetrievalRequestedEvent"] = "AtWork Retrieval",
        ["Uc4AtWorkDocumentListCompletedEvent"] = "DocumentListSaga",
        ["Uc4AtWorkDocumentRetrievedEvent"] = "DocumentRetrievalSaga",
        ["StartMainframeDocumentAggregationCommand"] = "MainframeDocumentAggregator",
        ["MainframeAppraisalListPartReceivedEvent"] = "MainframeListAggregator",
        ["MainframeDocumentChunkReceivedEvent"] = "MainframeDocumentAggregator",
        ["Uc4MainframeDocumentListCompletedEvent"] = "DocumentListSaga",
        ["Uc4AppraisalDocumentRetrievedEvent"] = "DocumentRetrievalSaga",
        ["Uc4DocumentListSagaTimeoutMessage"] = "DocumentListSaga",
        ["Uc4DocumentRetrievalSagaTimeoutMessage"] = "DocumentRetrievalSaga",
        ["Uc4MainframeListAggregatorTimeoutMessage"] = "MainframeListAggregator",
        ["Uc4MainframeDocumentAggregatorTimeoutMessage"] = "MainframeDocumentAggregator",
    };

    private static readonly HashSet<string> SuppressedOutgoing = new(StringComparer.OrdinalIgnoreCase);

    public static string ResolveEndpoint(string? ep) =>
        ep is not null && EndpointToParticipant.TryGetValue(ep, out var label) ? label : ep ?? "?";

    public static string ResolveIncomingFrom(string messageType, string? originatingEndpoint)
        => string.Equals(messageType, "ProcessAppraisalStatusUpdateCommand", StringComparison.OrdinalIgnoreCase)
            ? "API"
            : ResolveEndpoint(originatingEndpoint);

    public static string ResolveOutgoingTo(string messageType)
        => MessageTypeToPrimarySubscriber.TryGetValue(messageType, out var p) ? p : "broadcast";

    public static bool ShouldSkipOutgoing(string messageType)
        => SuppressedOutgoing.Contains(messageType);
}

public sealed class AppraisalEDAFlowIncomingBehavior : Behavior<IIncomingLogicalMessageContext>
{
    private readonly ILogger<AppraisalEDAFlowIncomingBehavior> _logger;

    public AppraisalEDAFlowIncomingBehavior(ILogger<AppraisalEDAFlowIncomingBehavior> logger)
        => _logger = logger;

    public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
    {
        var headers = context.MessageHeaders;
        var originatingEndpoint = GetHeader(headers, NServiceBus.Headers.OriginatingEndpoint);
        var enclosedTypes = GetHeader(headers, NServiceBus.Headers.EnclosedMessageTypes);
        var messageType = enclosedTypes is not null
            ? enclosedTypes.Split(',')[0].Split('.').Last().Trim()
            : context.Message.MessageType.Name;

        // UC4 falls back to correlation/request ids for flow tracing
        var appraisalId = ExtractStringProperty(context.Message.Instance, "AppraisalId")
            ?? ExtractStringProperty(context.Message.Instance, "CorrelationId")
            ?? ExtractStringProperty(context.Message.Instance, "RequestId");
        if (!string.IsNullOrWhiteSpace(appraisalId))
        {
            var from = AppraisalParticipantMap.ResolveIncomingFrom(messageType, originatingEndpoint);
            var to = AppraisalParticipantMap.ResolveEndpoint(AppraisalParticipantMap.CurrentEndpoint);

            using var _1 = LogContext.PushProperty("EDA_Event", "EDA_FLOW");
            using var _2 = LogContext.PushProperty("EDA_IssuanceId", appraisalId); // keep key name for Loki compat
            using var _3 = LogContext.PushProperty("EDA_MessageType", messageType);
            using var _4 = LogContext.PushProperty("EDA_From", from);
            using var _5 = LogContext.PushProperty("EDA_To", to);
            using var _6 = LogContext.PushProperty("EDA_Direction", "consumed");
            using var _7 = LogContext.PushProperty("EDA_Stack", "dotnet");
            using var _8 = LogContext.PushProperty("EDA_Topic", $"nsb.{messageType.ToLowerInvariant()}");

            _logger.LogInformation("EDA_FLOW {EDA_MessageType} {EDA_From} -> {EDA_To}", messageType, from, to);
        }

        await next().ConfigureAwait(false);
    }

    private static string? GetHeader(IReadOnlyDictionary<string, string> h, string key)
        => h.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    private static string? ExtractStringProperty(object? msg, string name)
        => msg?.GetType().GetProperty(name)?.GetValue(msg)?.ToString();
}

public sealed class AppraisalEDAFlowOutgoingBehavior : Behavior<IOutgoingLogicalMessageContext>
{
    private readonly ILogger<AppraisalEDAFlowOutgoingBehavior> _logger;

    public AppraisalEDAFlowOutgoingBehavior(ILogger<AppraisalEDAFlowOutgoingBehavior> logger)
        => _logger = logger;

    public override async Task Invoke(IOutgoingLogicalMessageContext context, Func<Task> next)
    {
        var messageType = context.Message.MessageType.Name;
        var appraisalId = ExtractStringProperty(context.Message.Instance, "AppraisalId")
            ?? ExtractStringProperty(context.Message.Instance, "CorrelationId")
            ?? ExtractStringProperty(context.Message.Instance, "RequestId");

        if (!string.IsNullOrWhiteSpace(appraisalId) && !AppraisalParticipantMap.ShouldSkipOutgoing(messageType))
        {
            var from = AppraisalParticipantMap.ResolveEndpoint(AppraisalParticipantMap.CurrentEndpoint);
            var to = AppraisalParticipantMap.ResolveOutgoingTo(messageType);

            using var _1 = LogContext.PushProperty("EDA_Event", "EDA_FLOW");
            using var _2 = LogContext.PushProperty("EDA_IssuanceId", appraisalId);
            using var _3 = LogContext.PushProperty("EDA_MessageType", messageType);
            using var _4 = LogContext.PushProperty("EDA_From", from);
            using var _5 = LogContext.PushProperty("EDA_To", to);
            using var _6 = LogContext.PushProperty("EDA_Direction", "published");
            using var _7 = LogContext.PushProperty("EDA_Stack", "dotnet");
            using var _8 = LogContext.PushProperty("EDA_Topic", $"nsb.{messageType.ToLowerInvariant()}");

            _logger.LogInformation("EDA_FLOW {EDA_MessageType} {EDA_From} -> {EDA_To}", messageType, from, to);
        }

        await next().ConfigureAwait(false);
    }

    private static string? ExtractStringProperty(object? msg, string name)
        => msg?.GetType().GetProperty(name)?.GetValue(msg)?.ToString();
}
