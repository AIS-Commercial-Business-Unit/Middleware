using System.Net.Http.Json;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;
using dotnet_platform_compliance.Infrastructure;

namespace dotnet_platform_compliance.Handlers;

public sealed class ComplianceCheckHandler : IHandleMessages<PolicyIssuanceInitiatedEvent>
{
    public async Task Handle(PolicyIssuanceInitiatedEvent message, IMessageHandlerContext context)
    {
        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            ComplianceRuntime.Logger?.LogInformation(
                "[EDA subscriber] dotnet-platform-compliance received PolicyIssuanceInitiatedEvent — issuanceId={IssuanceId}",
                message.IssuanceId);
        }

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

            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                ComplianceRuntime.Logger?.LogInformation(
                    "ComplianceCheck HTTP success — issuanceId={IssuanceId} url={Url} httpStatus={StatusCode}",
                    message.IssuanceId,
                    ComplianceRuntime.ComplianceCheckUrl,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                ComplianceRuntime.Logger?.LogError(
                    ex,
                    "ComplianceCheck HTTP failed — issuanceId={IssuanceId} url={Url}",
                    message.IssuanceId,
                    ComplianceRuntime.ComplianceCheckUrl);
            }

            var blockedEvent = new ComplianceBlockedEvent
            {
                IssuanceId = message.IssuanceId,
                AccountId = message.AccountId,
                CheckId = $"CHK-{message.IssuanceId[..8]}",
                Reason = $"Compliance service unreachable: {ex.Message}",
                BlockedAt = DateTimeOffset.UtcNow
            };

            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                ComplianceRuntime.Logger?.LogInformation(
                    "[EDA publish] dotnet-platform-compliance publishing ComplianceBlockedEvent — issuanceId={IssuanceId}",
                    message.IssuanceId);
            }

            await context.Publish(blockedEvent).ConfigureAwait(false);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(context.CancellationToken).ConfigureAwait(false);
        var blocked = !response.IsSuccessStatusCode || body.Contains("block", StringComparison.OrdinalIgnoreCase);

        if (blocked)
        {
            var blockedEvent = new ComplianceBlockedEvent
            {
                IssuanceId = message.IssuanceId,
                AccountId = message.AccountId,
                CheckId = $"CHK-{message.IssuanceId[..8]}",
                Reason = response.IsSuccessStatusCode
                    ? "Compliance blocked by upstream service."
                    : $"Compliance service call failed: {body}",
                BlockedAt = DateTimeOffset.UtcNow
            };

            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                ComplianceRuntime.Logger?.LogInformation(
                    "[EDA publish] dotnet-platform-compliance publishing ComplianceBlockedEvent — issuanceId={IssuanceId}",
                    message.IssuanceId);
            }

            await context.Publish(blockedEvent).ConfigureAwait(false);
            return;
        }

        var clearedEvent = new ComplianceClearedEvent
        {
            IssuanceId = message.IssuanceId,
            AccountId = message.AccountId,
            CheckId = $"CHK-{message.IssuanceId[..8]}",
            ClearedAt = DateTimeOffset.UtcNow
        };

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            ComplianceRuntime.Logger?.LogInformation(
                "[EDA publish] dotnet-platform-compliance publishing ComplianceClearedEvent — issuanceId={IssuanceId}",
                message.IssuanceId);
        }

        await context.Publish(clearedEvent).ConfigureAwait(false);
    }
}
