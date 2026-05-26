using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;
using dotnet_kafka_bridge.Infrastructure;

namespace dotnet_kafka_bridge.Handlers;

public sealed class PolicyIssuedEventHandler :
    IHandleMessages<PolicyIssuedEvent>,
    IHandleMessages<IssuanceFailedEvent>
{
    public async Task Handle(PolicyIssuedEvent message, IMessageHandlerContext context)
    {
        const string topic = "policy.events.policy-issued";
        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            KafkaBridgeRuntime.Logger?.LogInformation(
                "KafkaBridge forwarding PolicyIssuedEvent — issuanceId={IssuanceId} topic={Topic} policyNumbers={PolicyNumbers}",
                message.IssuanceId, topic, string.Join(",", message.PolicyNumbers ?? []));
        }

        await KafkaBridgeRuntime.PublishAsync(topic, message, context.CancellationToken).ConfigureAwait(false);

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            KafkaBridgeRuntime.Logger?.LogInformation(
                "KafkaBridge event forwarded — issuanceId={IssuanceId} topic={Topic}",
                message.IssuanceId, topic);
        }
    }

    public async Task Handle(IssuanceFailedEvent message, IMessageHandlerContext context)
    {
        const string topic = "policy.events.issuance-failed";
        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            KafkaBridgeRuntime.Logger?.LogWarning(
                "KafkaBridge forwarding IssuanceFailedEvent — issuanceId={IssuanceId} topic={Topic} reason={Reason}",
                message.IssuanceId, topic, message.Reason);
        }

        await KafkaBridgeRuntime.PublishAsync(topic, message, context.CancellationToken).ConfigureAwait(false);
    }
}
