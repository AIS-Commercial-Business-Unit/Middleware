using System.Net.Http.Json;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;
using dotnet_customer_identity.Infrastructure;

namespace dotnet_customer_identity.Handlers;

public sealed class UpdateCustomerRecordHandler : IHandleMessages<UpdateCustomerRecordCommand>
{
    public async Task Handle(UpdateCustomerRecordCommand message, IMessageHandlerContext context)
    {
        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            CustomerIdentityRuntime.Logger?.LogInformation(
                "UpdateCustomerRecord started — issuanceId={IssuanceId} accountId={AccountId} policyNumbers={PolicyNumbers} targetPas={TargetPas}",
                message.IssuanceId, message.AccountId, string.Join(",", message.PolicyNumbers ?? []), message.TargetPas);
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
                    "UpdateCustomerRecord HTTP response — issuanceId={IssuanceId} httpStatus={StatusCode}",
                    message.IssuanceId, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                CustomerIdentityRuntime.Logger?.LogWarning(ex,
                    "UpdateCustomerRecord HTTP call failed (non-fatal) — issuanceId={IssuanceId} — continuing",
                    message.IssuanceId);
            }
        }

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            CustomerIdentityRuntime.Logger?.LogInformation(
                "CustomerRecord UPDATED — issuanceId={IssuanceId} fieldsUpdated={Fields}",
                message.IssuanceId, "policyNumbers,targetPas");
        }

        await context.Publish(new CustomerUpdatedEvent
        {
            IssuanceId = message.IssuanceId,
            AccountId = message.AccountId,
            FieldsUpdated = ["policyNumbers", "targetPas"],
            UpdatedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);
    }
}
