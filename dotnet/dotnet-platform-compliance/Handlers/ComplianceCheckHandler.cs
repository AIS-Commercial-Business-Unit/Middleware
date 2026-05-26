using System.Net.Http.Json;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using NServiceBus;
using dotnet_platform_compliance.Infrastructure;

namespace dotnet_platform_compliance.Handlers;

public sealed class ComplianceCheckHandler : IHandleMessages<RequestComplianceCheckCommand>
{
    public async Task Handle(RequestComplianceCheckCommand message, IMessageHandlerContext context)
    {
        HttpResponseMessage response;
        try
        {
            response = await ComplianceRuntime.HttpClient.PostAsJsonAsync(
                ComplianceRuntime.ComplianceCheckUrl,
                new
                {
                    issuanceId = message.IssuanceId,
                    accountId = message.AccountId,
                    policyTypeCode = message.PolicyTypeCode,
                    requestedAt = message.RequestedAt
                },
                context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await context.Publish(new ComplianceBlockedEvent
            {
                IssuanceId = message.IssuanceId,
                AccountId = message.AccountId,
                CheckId = $"CHK-{message.IssuanceId[..8]}",
                Reason = $"Compliance service unreachable: {ex.Message}",
                BlockedAt = DateTimeOffset.UtcNow
            }).ConfigureAwait(false);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(context.CancellationToken).ConfigureAwait(false);
        var blocked = !response.IsSuccessStatusCode || body.Contains("block", StringComparison.OrdinalIgnoreCase);

        if (blocked)
        {
            await context.Publish(new ComplianceBlockedEvent
            {
                IssuanceId = message.IssuanceId,
                AccountId = message.AccountId,
                CheckId = $"CHK-{message.IssuanceId[..8]}",
                Reason = response.IsSuccessStatusCode ? "Compliance blocked by upstream service." : $"Compliance service call failed: {body}",
                BlockedAt = DateTimeOffset.UtcNow
            }).ConfigureAwait(false);
            return;
        }

        await context.Publish(new ComplianceClearedEvent
        {
            IssuanceId = message.IssuanceId,
            AccountId = message.AccountId,
            CheckId = $"CHK-{message.IssuanceId[..8]}",
            ClearedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);
    }
}