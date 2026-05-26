using System.Net.Http.Json;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;
using dotnet_platform_integration.Infrastructure;

namespace dotnet_platform_integration.Handlers;

public sealed class PasGatewayHandler : IHandleMessages<IssueToAdminSystemCommand>
{
    public async Task Handle(IssueToAdminSystemCommand message, IMessageHandlerContext context)
    {
        var (targetPas, url) = ResolveTarget(message.PolicyTypeCode);

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            PasGatewayRuntime.Logger?.LogInformation(
                "PasGateway routing — issuanceId={IssuanceId} policyTypeCode={PolicyTypeCode} targetPas={TargetPas}",
                message.IssuanceId,
                message.PolicyTypeCode,
                targetPas);
        }

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
            var failedEvent = new PolicyAdminSystemCallFailedEvent
            {
                IssuanceId = message.IssuanceId,
                AccountId = message.AccountId,
                Reason = $"{targetPas} unreachable: {ex.Message}",
                FailedAt = DateTimeOffset.UtcNow
            };

            await context.Publish(failedEvent).ConfigureAwait(false);

            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                PasGatewayRuntime.Logger?.LogError(
                    ex,
                    "PasGateway FAILED — issuanceId={IssuanceId} targetPas={TargetPas} httpStatus={StatusCode}",
                    message.IssuanceId,
                    targetPas,
                    "EXCEPTION");
            }

            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(context.CancellationToken).ConfigureAwait(false);
            var failedEvent = new PolicyAdminSystemCallFailedEvent
            {
                IssuanceId = message.IssuanceId,
                AccountId = message.AccountId,
                Reason = $"{targetPas} call failed: {error}",
                FailedAt = DateTimeOffset.UtcNow
            };

            await context.Publish(failedEvent).ConfigureAwait(false);

            using (LogContext.PushProperty("issuanceId", message.IssuanceId))
            {
                PasGatewayRuntime.Logger?.LogError(
                    "PasGateway FAILED — issuanceId={IssuanceId} targetPas={TargetPas} httpStatus={StatusCode}",
                    message.IssuanceId,
                    targetPas,
                    (int)response.StatusCode);
            }

            return;
        }

        var policyNumber = targetPas switch
        {
            "ForeFront" => $"FF-{message.IssuanceId[..8].ToUpperInvariant()}",
            "DuckCreek-Commercial" => $"DC-COMM-{message.IssuanceId[..8].ToUpperInvariant()}",
            _ => $"DC-PERS-{message.IssuanceId[..8].ToUpperInvariant()}"
        };

        var responseReceivedEvent = new PolicyAdminSystemResponseReceivedEvent
        {
            IssuanceId = message.IssuanceId,
            AccountId = message.AccountId,
            TargetPas = targetPas,
            PolicyNumbers = [policyNumber],
            ReceivedAt = DateTimeOffset.UtcNow
        };

        await context.Publish(responseReceivedEvent).ConfigureAwait(false);

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            PasGatewayRuntime.Logger?.LogInformation(
                "PasGateway SUCCESS — issuanceId={IssuanceId} policyNumber={PolicyNumber} targetPas={TargetPas}",
                responseReceivedEvent.IssuanceId,
                policyNumber,
                responseReceivedEvent.TargetPas);
        }
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
