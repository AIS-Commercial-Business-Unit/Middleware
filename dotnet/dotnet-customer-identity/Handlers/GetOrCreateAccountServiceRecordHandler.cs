using System.Net.Http.Json;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;
using dotnet_customer_identity.Infrastructure;

namespace dotnet_customer_identity.Handlers;

public sealed class GetOrCreateAccountServiceRecordHandler : IHandleMessages<GetOrCreateAccountServiceRecordCommand>
{
    public async Task Handle(GetOrCreateAccountServiceRecordCommand message, IMessageHandlerContext context)
    {
        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            CustomerIdentityRuntime.Logger?.LogInformation(
                "GetOrCreateAccountServiceRecord started — issuanceId={IssuanceId} accountId={AccountId} url={Url}",
                message.IssuanceId, message.AccountId, CustomerIdentityRuntime.AccountServiceUrl);
        }

        try
        {
            var response = await CustomerIdentityRuntime.HttpClient.PostAsJsonAsync(
                    CustomerIdentityRuntime.AccountServiceUrl,
                    new { issuanceId = message.IssuanceId, accountId = message.AccountId },
                    context.CancellationToken)
                .ConfigureAwait(false);
            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                CustomerIdentityRuntime.Logger?.LogInformation(
                    "GetOrCreateAccountServiceRecord HTTP response — issuanceId={IssuanceId} httpStatus={StatusCode}",
                    message.IssuanceId, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                CustomerIdentityRuntime.Logger?.LogWarning(ex,
                    "GetOrCreateAccountServiceRecord HTTP call failed (non-fatal) — issuanceId={IssuanceId} — continuing",
                    message.IssuanceId);
            }
        }

        var accountServiceRequestNumber = $"ASR-{message.IssuanceId[..8].ToUpperInvariant()}";

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            CustomerIdentityRuntime.Logger?.LogInformation(
                "AccountServiceRecord RETRIEVED — issuanceId={IssuanceId} accountServiceRequestNumber={AccountServiceRequestNumber}",
                message.IssuanceId, accountServiceRequestNumber);
        }

        await context.Publish(new AccountServiceRecordRetrievedEvent
        {
            IssuanceId = message.IssuanceId,
            AccountId = message.AccountId,
            AccountServiceRequestNumber = accountServiceRequestNumber,
            RetrievedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);
    }
}
