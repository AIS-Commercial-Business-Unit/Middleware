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
                "BillingAssociation started — issuanceId={IssuanceId} accountId={AccountId} " +
                "accountServiceRequestNumber={AccountServiceRequestNumber} billingChannel={BillingChannel}",
                message.IssuanceId, message.AccountId, message.AccountServiceRequestNumber, message.BillingChannel);
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
                    "BillingAssociation HTTP call completed — issuanceId={IssuanceId} httpStatus={StatusCode}",
                    message.IssuanceId, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                BillingRuntime.Logger?.LogWarning(ex,
                    "BillingAssociation HTTP call failed (non-fatal) — issuanceId={IssuanceId} — continuing with event publish",
                    message.IssuanceId);
            }
        }

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            BillingRuntime.Logger?.LogInformation(
                "BillingAssociation CREATED — issuanceId={IssuanceId} billingChannel={BillingChannel}",
                message.IssuanceId, message.BillingChannel);
        }

        await context.Publish(new BillingAssociationCreatedEvent
        {
            IssuanceId = message.IssuanceId,
            AccountId = message.AccountId,
            AccountServiceRequestNumber = message.AccountServiceRequestNumber,
            BillingChannel = message.BillingChannel,
            CreatedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);
    }
}
