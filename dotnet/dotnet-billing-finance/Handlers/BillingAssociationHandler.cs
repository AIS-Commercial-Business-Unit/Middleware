using System.Net.Http.Json;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;
using dotnet_billing_finance.Infrastructure;

namespace dotnet_billing_finance.Handlers;

public sealed class BillingAssociationHandler : IHandleMessages<PolicyAdminSystemResponseReceivedEvent>
{
    public async Task Handle(PolicyAdminSystemResponseReceivedEvent message, IMessageHandlerContext context)
    {
        const string billingChannel = "DirectBill";

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            BillingRuntime.Logger?.LogInformation(
                "[EDA subscriber] dotnet-billing-finance received PolicyAdminSystemResponseReceivedEvent — issuanceId={IssuanceId}",
                message.IssuanceId);
        }

        try
        {
            var response = await BillingRuntime.HttpClient.PostAsJsonAsync(
                    BillingRuntime.BillingUrl,
                    new
                    {
                        issuanceId = message.IssuanceId,
                        accountId = message.AccountId,
                        accountServiceRequestNumber = message.AccountServiceRequestNumber,
                        billingChannel
                    },
                    context.CancellationToken)
                .ConfigureAwait(false);

            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                BillingRuntime.Logger?.LogInformation(
                    "BillingAssociation HTTP success — issuanceId={IssuanceId} httpStatus={StatusCode}",
                    message.IssuanceId,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                BillingRuntime.Logger?.LogWarning(
                    ex,
                    "BillingAssociation HTTP failed (non-fatal) — issuanceId={IssuanceId}",
                    message.IssuanceId);
            }
        }

        var createdEvent = new BillingAssociationCreatedEvent
        {
            IssuanceId = message.IssuanceId,
            AccountId = message.AccountId,
            AccountServiceRequestNumber = message.AccountServiceRequestNumber,
            BillingChannel = billingChannel,
            CreatedAt = DateTimeOffset.UtcNow
        };

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            BillingRuntime.Logger?.LogInformation(
                "[EDA publish] dotnet-billing-finance publishing BillingAssociationCreatedEvent — issuanceId={IssuanceId}",
                message.IssuanceId);
        }

        await context.Publish(createdEvent).ConfigureAwait(false);
    }
}
