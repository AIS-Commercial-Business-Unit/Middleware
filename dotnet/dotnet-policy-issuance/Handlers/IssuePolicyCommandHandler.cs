using dotnet_policy_issuance.Controllers;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Models;

namespace dotnet_policy_issuance.Handlers;

public sealed class IssuePolicyCommandHandler
{
    public IssuePolicyCommand Normalize(PolicyIssuanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var policies = request.Policies.Count == 0
            ? [new PolicyItem { PolicyTypeCode = 1, PolicyTypeSubCode = 0 }]
            : request.Policies.Select(policy => new PolicyItem
            {
                PolicyTypeCode = policy.PolicyTypeCode,
                PolicyTypeSubCode = policy.PolicyTypeSubCode
            }).ToList();

        var accountId = string.IsNullOrWhiteSpace(request.AccountId)
            ? $"ACC-{Guid.NewGuid():N}"[..12].ToUpperInvariant()
            : request.AccountId.Trim();

        return new IssuePolicyCommand
        {
            IssuanceId = string.IsNullOrWhiteSpace(request.IssuanceId) ? Guid.NewGuid().ToString() : request.IssuanceId.Trim(),
            AccountId = accountId,
            Policies = policies,
            SubmittingChannel = string.IsNullOrWhiteSpace(request.SubmittingChannel) ? "DirectRequest" : request.SubmittingChannel.Trim(),
            RequestedAt = request.RequestedAt ?? DateTimeOffset.UtcNow,
            BatchId = request.BatchId,
            RecordId = request.RecordId
        };
    }
}
