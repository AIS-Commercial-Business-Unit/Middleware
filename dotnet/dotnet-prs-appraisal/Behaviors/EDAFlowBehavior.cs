using Microsoft.Extensions.Logging;
using NServiceBus.Pipeline;
using Serilog.Context;

namespace dotnet_prs_appraisal.Behaviors;

/// <summary>
/// EDA_FLOW behaviors for dotnet-prs-appraisal.
/// Emit structured log entries consumed by Loki and rendered as sequence diagrams.
/// Uses "appraisalId" as the correlation key (not "issuanceId").
///
/// Participant IDs emitted into EDA_From / EDA_To / EDA_Handler match the
/// PARTICIPANTS constant in the platform-ui sequence diagram page so that
/// lifelines resolve correctly.  Never emit "broadcast" or "AllSubscribers"
/// — fan-out is shown through the handled entries of each subscriber.
/// </summary>
file static class AppraisalCurrentHandlerContext
{
    /// <summary>
    /// Tracks the short handler/saga name while a handler is executing so the
    /// outgoing behavior can set EDA_From to the actual sender rather than the
    /// endpoint name.  Flows correctly across await boundaries via AsyncLocal.
    /// </summary>
    internal static readonly AsyncLocal<string?> HandlerTypeName = new();
}

file static class AppraisalParticipantMap
{
    public const string CurrentEndpoint = "dotnet-prs-appraisal";

    /// <summary>NServiceBus routing artefacts that must never appear as diagram lifelines.</summary>
    private static readonly HashSet<string> SuppressedParticipants = new(StringComparer.OrdinalIgnoreCase)
    {
        "broadcast", "AllSubscribers",
    };

    private static readonly Dictionary<string, string> EndpointToParticipant = new(StringComparer.OrdinalIgnoreCase)
    {
        [CurrentEndpoint]                  = "AppraisalDocumentsController",
        ["dotnet-customer-identity"]       = "CustomerIdentity",
        ["dotnet-platform-integration"]    = "Integration",
        ["dotnet-platform-notification"]   = "Notification",
        ["AtWork"]                         = "AtWork",
        ["Mainframe"]                      = "Mainframe",
        ["deipde07-mq-simulator"]          = "Mainframe",
    };

    private static readonly Dictionary<string, string> HandlerToParticipant = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AppraisalDocumentsController"]       = "AppraisalDocumentsController",
        ["DocumentListSaga"]                   = "DocumentListSaga",
        ["DocumentRetrievalSaga"]              = "DocumentRetrievalSaga",
        ["MainframeListAggregatorSaga"]        = "MainframeListAggregator",
        ["MainframeDocumentAggregatorSaga"]    = "MainframeDocumentAggregator",
        ["MainframeListAccumulatorHandler"]    = "MainframeListAggregator",
        ["MainframeDocumentAccumulatorHandler"] = "MainframeDocumentAggregator",
        ["AtWorkDocumentListHandler"]          = "AtWorkDocumentListHandler",
        ["AtWorkDocumentRetrievalHandler"]     = "AtWorkDocumentRetrievalHandler",
        ["AppraisalStatusUpdateHandler"]       = "AppraisalDocumentsController",
        ["AppraisalReceivedSagaRoute"]         = "AppraisalDocumentsController",
    };

    /// <summary>
    /// All NServiceBus messages in this endpoint are intra-endpoint: both sender and
    /// receiver live in dotnet-prs-appraisal.  The incoming handler behavior
    /// (AppraisalEDAFlowHandlerInvokeBehavior) already logs a handled entry for every
    /// message, with the correct FROM participant via the EDA-Publisher header stamped
    /// by the outgoing behavior.  Populating this map caused the outgoing behavior to
    /// also log a published entry, producing two EDA_FLOW rows per message and doubling
    /// the arrows on the ops sequence diagram (e.g. 6 arrows for 3 mainframe reply events).
    /// Keep this empty; the EDA-Publisher header is still stamped unconditionally.
    /// </summary>
    private static readonly Dictionary<string, string> MessageTypeToKnownTarget = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> SuppressedOutgoing = new(StringComparer.OrdinalIgnoreCase)
    {
        "DocumentListSagaTimeoutMessage",
        "DocumentRetrievalSagaTimeoutMessage",
        "MainframeListAggregatorTimeoutMessage",
        "MainframeDocumentAggregatorTimeoutMessage",
    };

    /// <summary>
    /// Timeout message types that must never appear as sequence diagram steps.
    /// These are NServiceBus internal scheduling artefacts — they fire when a
    /// saga deadline elapses, but carrying them on the diagram adds noise without
    /// conveying meaningful business information.
    /// </summary>
    private static readonly HashSet<string> SuppressedHandled = new(StringComparer.OrdinalIgnoreCase)
    {
        "DocumentListSagaTimeoutMessage",
        "DocumentRetrievalSagaTimeoutMessage",
        "MainframeListAggregatorTimeoutMessage",
        "MainframeDocumentAggregatorTimeoutMessage",
    };

    private static readonly HashSet<string> ApiOriginatedMessages = new(StringComparer.OrdinalIgnoreCase)
    {
        "ProcessAppraisalStatusUpdateCommand",
        "GetAppraisalDocumentListCommand",
        "RetrieveAppraisalDocumentCommand",
    };

    public static string ResolveEndpoint(string? ep) =>
        ep is not null && EndpointToParticipant.TryGetValue(ep, out var label) ? label : ep ?? "?";

    public static string ResolveHandler(string handlerTypeName) =>
        HandlerToParticipant.TryGetValue(handlerTypeName, out var label) ? label : handlerTypeName;

    public static string ResolveIncomingFrom(string messageType, string? originatingEndpoint)
        => ApiOriginatedMessages.Contains(messageType) ? "AppraisalDocumentsController" : ResolveEndpoint(originatingEndpoint);

    /// <summary>
    /// Returns the known destination for a point-to-point message, or null for
    /// publish-subscribe events where the subscriber set is resolved at runtime.
    /// Returning null causes the outgoing behavior to skip the log entry; the
    /// fan-out will be captured by each subscriber's handled entry instead.
    /// </summary>
    public static string? ResolveOutgoingTo(string messageType)
        => MessageTypeToKnownTarget.TryGetValue(messageType, out var p) ? p : null;

    public static bool IsSuppressedParticipant(string participantId)
        => SuppressedParticipants.Contains(participantId);

    public static bool ShouldSkipOutgoing(string messageType)
        => SuppressedOutgoing.Contains(messageType);

    public static bool ShouldSkipHandled(string messageType)
        => SuppressedHandled.Contains(messageType);
}

file static class AppraisalEDAFlowLog
{
    private static readonly HashSet<string> ExcludedPayloadFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ContentBase64", "Content", "AccumulatedChunksJson", "ChunkPayload", "AtWorkContent"
    };

    public static string? SafeSerializePayload(object? message)
    {
        if (message is null) return null;
        try
        {
            var dict = message.GetType().GetProperties(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.CanRead && !ExcludedPayloadFields.Contains(p.Name))
                .ToDictionary(p => p.Name, p => p.GetValue(message));
            return System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return null;
        }
    }

    public static string? GetHeader(IReadOnlyDictionary<string, string> headers, string key)
        => headers.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public static string ResolveMessageType(Type fallbackType, IReadOnlyDictionary<string, string> headers)
    {
        var enclosedTypes = GetHeader(headers, NServiceBus.Headers.EnclosedMessageTypes);
        return enclosedTypes is not null
            ? enclosedTypes.Split(',')[0].Split('.').Last().Trim()
            : fallbackType.Name;
    }

    public static string? ExtractCorrelationId(object? message)
        => ExtractStringProperty(message, "AppraisalId")
            ?? ExtractStringProperty(message, "CorrelationId")
            ?? ExtractStringProperty(message, "RequestId");

    public static void Write(
        ILogger logger,
        string appraisalId,
        string messageType,
        string from,
        string to,
        string direction,
        string? handler = null,
        string? payload = null)
    {
        using var _1 = LogContext.PushProperty("EDA_Event", "EDA_FLOW");
        using var _2 = LogContext.PushProperty("EDA_IssuanceId", appraisalId);
        using var _3 = LogContext.PushProperty("EDA_MessageType", messageType);
        using var _4 = LogContext.PushProperty("EDA_From", from);
        using var _5 = LogContext.PushProperty("EDA_To", to);
        using var _6 = LogContext.PushProperty("EDA_Direction", direction);
        using var _7 = LogContext.PushProperty("EDA_Stack", "dotnet");
        using var _8 = LogContext.PushProperty("EDA_Topic", $"nsb.{messageType.ToLowerInvariant()}");
        using var _9 = handler is not null ? LogContext.PushProperty("EDA_Handler", handler) : null;
        using var _10 = payload is not null ? LogContext.PushProperty("EDA_Payload", payload) : null;
        logger.LogInformation("EDA_FLOW {EDA_MessageType} {EDA_From} -> {EDA_To}", messageType, from, to);
    }

    private static string? ExtractStringProperty(object? message, string name)
        => message?.GetType().GetProperty(name)?.GetValue(message)?.ToString();
}

public sealed class AppraisalEDAFlowHandlerInvokeBehavior : Behavior<IInvokeHandlerContext>
{
    private readonly ILogger<AppraisalEDAFlowHandlerInvokeBehavior> _logger;

    public AppraisalEDAFlowHandlerInvokeBehavior(ILogger<AppraisalEDAFlowHandlerInvokeBehavior> logger)
        => _logger = logger;

    public override async Task Invoke(IInvokeHandlerContext context, Func<Task> next)
    {
        var headers = context.Headers;
        var originatingEndpoint = AppraisalEDAFlowLog.GetHeader(headers, NServiceBus.Headers.OriginatingEndpoint);
        var messageType = AppraisalEDAFlowLog.ResolveMessageType(context.MessageMetadata.MessageType, headers);
        var appraisalId = AppraisalEDAFlowLog.ExtractCorrelationId(context.MessageBeingHandled);

        if (!string.IsNullOrWhiteSpace(appraisalId) && !AppraisalParticipantMap.ShouldSkipHandled(messageType))
        {
            var handlerTypeName = context.MessageHandler.HandlerType.Name;
            var handlerParticipant = AppraisalParticipantMap.ResolveHandler(handlerTypeName);

            // EDA-Publisher is set by AppraisalEDAFlowOutgoingBehavior on the
            // way out so subscribers know the specific handler that published
            // the event (NServiceBus OriginatingEndpoint only carries the
            // endpoint name, not the handler).  Fall back to endpoint-based
            // resolution for messages that arrive without this header.
            var edaPublisher = AppraisalEDAFlowLog.GetHeader(headers, "EDA-Publisher");
            var from = edaPublisher
                ?? AppraisalParticipantMap.ResolveIncomingFrom(messageType, originatingEndpoint);

            // Track current handler in AsyncLocal so the outgoing behavior can
            // attribute published/sent messages to the correct participant.
            var previous = AppraisalCurrentHandlerContext.HandlerTypeName.Value;
            AppraisalCurrentHandlerContext.HandlerTypeName.Value = handlerParticipant;
            try
            {
                var serializedPayload = AppraisalEDAFlowLog.SafeSerializePayload(context.MessageBeingHandled);
                AppraisalEDAFlowLog.Write(_logger, appraisalId, messageType, from, handlerParticipant, "handled", handlerTypeName, serializedPayload);
                await next().ConfigureAwait(false);
            }
            finally
            {
                AppraisalCurrentHandlerContext.HandlerTypeName.Value = previous;
            }
        }
        else
        {
            await next().ConfigureAwait(false);
        }
    }
}

public sealed class AppraisalEDAFlowOutgoingBehavior : Behavior<IOutgoingLogicalMessageContext>
{
    private readonly ILogger<AppraisalEDAFlowOutgoingBehavior> _logger;

    public AppraisalEDAFlowOutgoingBehavior(ILogger<AppraisalEDAFlowOutgoingBehavior> logger)
        => _logger = logger;

    public override async Task Invoke(IOutgoingLogicalMessageContext context, Func<Task> next)
    {
        var messageType = context.Message.MessageType.Name;
        var appraisalId = AppraisalEDAFlowLog.ExtractCorrelationId(context.Message.Instance);

        if (!string.IsNullOrWhiteSpace(appraisalId) && !AppraisalParticipantMap.ShouldSkipOutgoing(messageType))
        {
            // EDA-Publisher priority: (1) current handler context (inside a handler/saga),
            // (2) a header pre-set by the caller via SendOptions/PublishOptions (used by
            // background services like Artemis listeners to attribute the correct source),
            // (3) endpoint-level fallback.
            var from = AppraisalCurrentHandlerContext.HandlerTypeName.Value
                ?? AppraisalEDAFlowLog.GetHeader(context.Headers, "EDA-Publisher")
                ?? AppraisalParticipantMap.ResolveEndpoint(AppraisalParticipantMap.CurrentEndpoint);

            // Always stamp EDA-Publisher so subscribers can use the correct
            // "from" participant even for fan-out events where we skip the
            // outgoing log entry (NServiceBus only carries the endpoint name,
            // not the specific handler, in OriginatingEndpoint).
            context.Headers["EDA-Publisher"] = from;

            var knownTarget = AppraisalParticipantMap.ResolveOutgoingTo(messageType);

            // Only emit an outgoing entry for point-to-point messages with a
            // known target.  Fan-out events (null target) are represented by
            // the handled entries of each subscriber — emitting "→ broadcast"
            // adds noise without information.
            if (knownTarget is not null && !AppraisalParticipantMap.IsSuppressedParticipant(knownTarget))
            {
                AppraisalEDAFlowLog.Write(_logger, appraisalId, messageType, from, knownTarget, "published");
            }
        }

        await next().ConfigureAwait(false);
    }
}
