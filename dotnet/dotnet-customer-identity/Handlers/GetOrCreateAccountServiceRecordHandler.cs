using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;
using dotnet_customer_identity.Infrastructure;

namespace dotnet_customer_identity.Handlers;

public sealed class GetOrCreateAccountServiceRecordHandler : IHandleMessages<AccountLookupRequestedEvent>
{
    public async Task Handle(AccountLookupRequestedEvent message, IMessageHandlerContext context)
    {
        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            CustomerIdentityRuntime.Logger?.LogInformation(
                "[EDA subscriber] dotnet-customer-identity received AccountLookupRequestedEvent — issuanceId={IssuanceId}",
                message.IssuanceId);
        }

        try
        {
            var url = $"{CustomerIdentityRuntime.AccountServiceUrl}?issuanceId={Uri.EscapeDataString(message.IssuanceId)}&accountId={Uri.EscapeDataString(message.AccountId)}";
            var response = await CustomerIdentityRuntime.HttpClient.GetAsync(url, context.CancellationToken)
                .ConfigureAwait(false);

            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                CustomerIdentityRuntime.Logger?.LogInformation(
                    "GetOrCreateAccountRecord HTTP success — issuanceId={IssuanceId} httpStatus={StatusCode}",
                    message.IssuanceId,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                CustomerIdentityRuntime.Logger?.LogWarning(
                    ex,
                    "GetOrCreateAccountRecord HTTP failed (non-fatal) — issuanceId={IssuanceId}",
                    message.IssuanceId);
            }
        }

        var retrievedEvent = new AccountServiceRecordRetrievedEvent
        {
            IssuanceId = message.IssuanceId,
            AccountId = message.AccountId,
            AccountServiceRequestNumber = $"ASR-{message.IssuanceId[..8].ToUpperInvariant()}",
            RetrievedAt = DateTimeOffset.UtcNow
        };

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            CustomerIdentityRuntime.Logger?.LogInformation(
                "[EDA publish] dotnet-customer-identity publishing AccountServiceRecordRetrievedEvent — issuanceId={IssuanceId}",
                message.IssuanceId);
        }

        await context.Publish(retrievedEvent).ConfigureAwait(false);
    }
}
