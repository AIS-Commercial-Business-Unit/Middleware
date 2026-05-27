using System.Net.Http.Json;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;
using dotnet_customer_identity.Infrastructure;

namespace dotnet_customer_identity.Handlers;

public sealed class UpdateCustomerRecordHandler : IHandleMessages<PolicyAdminSystemResponseReceivedEvent>
{
    public async Task Handle(PolicyAdminSystemResponseReceivedEvent message, IMessageHandlerContext context)
    {
        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            CustomerIdentityRuntime.Logger?.LogInformation(
                "[EDA subscriber] dotnet-customer-identity received PolicyAdminSystemResponseReceivedEvent — issuanceId={IssuanceId}",
                message.IssuanceId);
        }

        try
        {
            var response = await CustomerIdentityRuntime.HttpClient.PostAsJsonAsync(
                    CustomerIdentityRuntime.CustomerServiceUrl,
                    new
                    {
                        issuanceId = message.IssuanceId,
                        accountId = message.AccountId,
                        policyNumbers = message.PolicyNumbers,
                        targetPas = message.TargetPas
                    },
                    context.CancellationToken)
                .ConfigureAwait(false);

            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                CustomerIdentityRuntime.Logger?.LogInformation(
                    "UpdateCustomerRecord HTTP success — issuanceId={IssuanceId} httpStatus={StatusCode} policyNumbers={PolicyNumbers}",
                    message.IssuanceId,
                    (int)response.StatusCode,
                    string.Join(",", message.PolicyNumbers ?? []));
            }
        }
        catch (Exception ex)
        {
            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                CustomerIdentityRuntime.Logger?.LogWarning(
                    ex,
                    "UpdateCustomerRecord HTTP failed (non-fatal) — issuanceId={IssuanceId} targetPas={TargetPas}",
                    message.IssuanceId,
                    message.TargetPas);
            }
        }

        var updatedEvent = new CustomerUpdatedEvent
        {
            IssuanceId = message.IssuanceId,
            AccountId = message.AccountId,
            FieldsUpdated = ["policyNumbers", "targetPas"],
            UpdatedAt = DateTimeOffset.UtcNow
        };

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            CustomerIdentityRuntime.Logger?.LogInformation(
                "[EDA publish] dotnet-customer-identity publishing CustomerUpdatedEvent — issuanceId={IssuanceId}",
                message.IssuanceId);
        }

        await context.Publish(updatedEvent).ConfigureAwait(false);

        var options = new SendOptions();
        options.SetDestination("dotnet-policy-issuance");
        options.DoNotEnforceBestPractices();
        await context.Send(updatedEvent, options).ConfigureAwait(false);
    }
}
