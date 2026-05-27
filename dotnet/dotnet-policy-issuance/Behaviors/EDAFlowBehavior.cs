using Microsoft.Extensions.Logging;
using NServiceBus.Pipeline;
using Serilog.Context;

namespace dotnet_policy_issuance.Behaviors;

/// <summary>
/// Two NServiceBus pipeline behaviors that emit EDA_FLOW structured log entries.
/// These are queried from Loki by the Platform UI to dynamically render the
/// sequence diagram.
/// </summary>
file static class ParticipantMap
{
    public const string CurrentEndpoint = "dotnet-policy-issuance";

    private static readonly Dictionary<string, string> EndpointToParticipant = new(StringComparer.OrdinalIgnoreCase)
    {
        [CurrentEndpoint] = "PolicyIssuance",
        ["dotnet-platform-compliance"] = "Compliance",
        ["dotnet-customer-identity"] = "CustomerIdentity",
        ["dotnet-platform-integration"] = "Integration",
        ["dotnet-billing-finance"] = "Billing",
        ["dotnet-platform-notification"] = "Notification",
    };

    private static readonly Dictionary<string, string> MessageTypeToPrimarySubscriber = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PolicyIssuanceInitiatedEvent"] = "Compliance",
        ["AccountLookupRequestedEvent"] = "CustomerIdentity",
        ["IssuePolicyRequestedEvent"] = "Integration",
        ["PolicyIssuedEvent"] = "Notification",
    };

    private static readonly HashSet<string> SuppressedOutgoingMessageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "IssuePolicyCommand",
        "IssuanceSagaStartedEvent",
        "PublishNotificationIntentCommand",
    };

    public static string ResolveEndpoint(string? endpointName) =>
        endpointName is not null && EndpointToParticipant.TryGetValue(endpointName, out var label)
            ? label
            : endpointName ?? "?";

    public static string ResolveIncomingFrom(string messageType, string? originatingEndpoint)
        => string.Equals(messageType, "IssuePolicyCommand", StringComparison.OrdinalIgnoreCase)
            ? "API"
            : ResolveEndpoint(originatingEndpoint);

    public static string ResolveOutgoingTo(string messageType)
        => MessageTypeToPrimarySubscriber.TryGetValue(messageType, out var participant)
            ? participant
            : "broadcast";

    public static bool ShouldSkipOutgoing(string messageType)
        => SuppressedOutgoingMessageTypes.Contains(messageType);
}

public sealed class EDAFlowIncomingBehavior : Behavior<IIncomingLogicalMessageContext>
{
    private readonly ILogger<EDAFlowIncomingBehavior> _logger;

    public EDAFlowIncomingBehavior(ILogger<EDAFlowIncomingBehavior> logger)
        => _logger = logger;

    public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
    {
        var headers = context.MessageHeaders;
        var originatingEndpoint = GetHeader(headers, NServiceBus.Headers.OriginatingEndpoint);
        var enclosedTypes = GetHeader(headers, NServiceBus.Headers.EnclosedMessageTypes);
        var messageType = enclosedTypes is not null
            ? enclosedTypes.Split(',')[0].Split('.').Last().Trim()
            : context.Message.MessageType.Name;

        var issuanceId = ExtractStringProperty(context.Message.Instance, "IssuanceId");
        if (!string.IsNullOrWhiteSpace(issuanceId))
        {
            var from = ParticipantMap.ResolveIncomingFrom(messageType, originatingEndpoint);
            var to = ParticipantMap.ResolveEndpoint(ParticipantMap.CurrentEndpoint);

            using var _1 = LogContext.PushProperty("EDA_Event", "EDA_FLOW");
            using var _2 = LogContext.PushProperty("EDA_IssuanceId", issuanceId);
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

    private static string? GetHeader(IReadOnlyDictionary<string, string> headers, string key)
        => headers.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static string? ExtractStringProperty(object? message, string propertyName)
    {
        if (message is null)
        {
            return null;
        }

        var property = message.GetType().GetProperty(propertyName);
        return property?.GetValue(message)?.ToString();
    }
}

public sealed class EDAFlowOutgoingBehavior : Behavior<IOutgoingLogicalMessageContext>
{
    private readonly ILogger<EDAFlowOutgoingBehavior> _logger;

    public EDAFlowOutgoingBehavior(ILogger<EDAFlowOutgoingBehavior> logger)
        => _logger = logger;

    public override async Task Invoke(IOutgoingLogicalMessageContext context, Func<Task> next)
    {
        var messageType = context.Message.MessageType.Name;
        var issuanceId = ExtractStringProperty(context.Message.Instance, "IssuanceId");

        if (!string.IsNullOrWhiteSpace(issuanceId) && !ParticipantMap.ShouldSkipOutgoing(messageType))
        {
            var from = ParticipantMap.ResolveEndpoint(ParticipantMap.CurrentEndpoint);
            var to = ParticipantMap.ResolveOutgoingTo(messageType);

            using var _1 = LogContext.PushProperty("EDA_Event", "EDA_FLOW");
            using var _2 = LogContext.PushProperty("EDA_IssuanceId", issuanceId);
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

    private static string? ExtractStringProperty(object? message, string propertyName)
    {
        if (message is null)
        {
            return null;
        }

        var property = message.GetType().GetProperty(propertyName);
        return property?.GetValue(message)?.ToString();
    }
}
