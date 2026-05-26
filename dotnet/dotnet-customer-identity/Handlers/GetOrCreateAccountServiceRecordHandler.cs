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
                "GetOrCreateAccountRecord started — issuanceId={IssuanceId}",
                message.IssuanceId);
        }

        try
        {
            // account-service stub uses GET with query params
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

        await context.Publish(retrievedEvent).ConfigureAwait(false);

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            CustomerIdentityRuntime.Logger?.LogInformation(
                "AccountServiceRecord RETRIEVED — issuanceId={IssuanceId} accountServiceRequestNumber={Asr}",
                retrievedEvent.IssuanceId,
                retrievedEvent.AccountServiceRequestNumber);
        }
    }
}
