using System.Net.Http.Json;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;
using dotnet_billing_finance.Infrastructure;

namespace dotnet_billing_finance.Handlers;

public sealed class BillingAssociationHandler : IHandleMessages<AssociateBillingAccountCommand>
{
    public async Task Handle(AssociateBillingAccountCommand message, IMessageHandlerContext context)
    {
        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            BillingRuntime.Logger?.LogInformation(
                "BillingAssociation started — issuanceId={IssuanceId} billingChannel={BillingChannel}",
                message.IssuanceId,
                message.BillingChannel);
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
                        billingChannel = message.BillingChannel
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
            BillingChannel = message.BillingChannel,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await context.Publish(createdEvent).ConfigureAwait(false);

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            BillingRuntime.Logger?.LogInformation(
                "BillingAssociation CREATED — issuanceId={IssuanceId}",
                createdEvent.IssuanceId);
        }
    }
}
