using System.Net.Http.Json;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using NServiceBus;
using dotnet_platform_integration.Infrastructure;

namespace dotnet_platform_integration.Handlers;

public sealed class PasGatewayHandler : IHandleMessages<IssueToAdminSystemCommand>
{
    public async Task Handle(IssueToAdminSystemCommand message, IMessageHandlerContext context)
    {
        var (targetPas, url) = ResolveTarget(message.PolicyTypeCode);

        HttpResponseMessage response;
        try
        {
            response = await PasGatewayRuntime.HttpClient.PostAsJsonAsync(
                url,
                new
                {
                    issuanceId = message.IssuanceId,
                    accountId = message.AccountId,
                    policyTypeCode = message.PolicyTypeCode,
                    policyTypeSubCode = message.PolicyTypeSubCode
                },
                context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await context.Publish(new PolicyAdminSystemCallFailedEvent
            {
                IssuanceId = message.IssuanceId,
                AccountId = message.AccountId,
                Reason = $"{targetPas} unreachable: {ex.Message}",
                FailedAt = DateTimeOffset.UtcNow
            }).ConfigureAwait(false);
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(context.CancellationToken).ConfigureAwait(false);
            await context.Publish(new PolicyAdminSystemCallFailedEvent
            {
                IssuanceId = message.IssuanceId,
                AccountId = message.AccountId,
                Reason = $"{targetPas} call failed: {error}",
                FailedAt = DateTimeOffset.UtcNow
            }).ConfigureAwait(false);
            return;
        }

        var policyNumber = targetPas switch
        {
            "ForeFront" => $"FF-{message.IssuanceId[..8].ToUpperInvariant()}",
            "DuckCreek-Commercial" => $"DC-COMM-{message.IssuanceId[..8].ToUpperInvariant()}",
            _ => $"DC-PERS-{message.IssuanceId[..8].ToUpperInvariant()}"
        };

        await context.Publish(new PolicyAdminSystemResponseReceivedEvent
        {
            IssuanceId = message.IssuanceId,
            AccountId = message.AccountId,
            TargetPas = targetPas,
            PolicyNumbers = [policyNumber],
            ReceivedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);
    }

    private static (string TargetPas, string Url) ResolveTarget(int policyTypeCode)
    {
        if ((policyTypeCode >= 1 && policyTypeCode <= 4) || (policyTypeCode >= 42 && policyTypeCode <= 47))
        {
            return ("DuckCreek-Commercial", PasGatewayRuntime.DuckCreekCommercialUrl);
        }

        if (policyTypeCode >= 10 && policyTypeCode <= 18)
        {
            return ("ForeFront", PasGatewayRuntime.ForeFrontUrl);
        }

        return ("DuckCreek-Personal", PasGatewayRuntime.DuckCreekPersonalUrl);
    }
}