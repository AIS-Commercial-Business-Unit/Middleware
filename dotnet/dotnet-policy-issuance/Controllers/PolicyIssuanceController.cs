using dotnet_policy_issuance.Domain;
using dotnet_policy_issuance.Handlers;
using dotnet_policy_issuance.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NServiceBus;

namespace dotnet_policy_issuance.Controllers;

[ApiController]
[Route("api/v1/policies")]
public sealed class PolicyIssuanceController : ControllerBase
{
    private readonly IssuePolicyCommandHandler _commandHandler;
    private readonly IMessageSession _messageSession;
    private readonly IIssuanceSagaRecordRepository _repository;
    private readonly ILogger<PolicyIssuanceController> _logger;

    public PolicyIssuanceController(
        IssuePolicyCommandHandler commandHandler,
        IMessageSession messageSession,
        IIssuanceSagaRecordRepository repository,
        ILogger<PolicyIssuanceController> logger)
    {
        _commandHandler = commandHandler;
        _messageSession = messageSession;
        _repository = repository;
        _logger = logger;
    }

    [HttpPost("issue")]
    public async Task<IActionResult> IssuePolicy([FromBody] PolicyIssuanceRequest request, CancellationToken cancellationToken)
    {
        var command = _commandHandler.Normalize(request);
        _logger.LogInformation(
            "IssuePolicy received — issuanceId={IssuanceId} accountId={AccountId} channel={Channel}",
            command.IssuanceId,
            command.AccountId,
            command.SubmittingChannel);

        var firstPolicy = command.Policies.First();

        await _repository.UpsertAsync(new IssuanceSagaRecord
        {
            IssuanceId = command.IssuanceId,
            AccountId = command.AccountId,
            Status = "Initiated",
            PolicyTypeCode = firstPolicy.PolicyTypeCode,
            PolicyTypeSubCode = firstPolicy.PolicyTypeSubCode,
            RequestedAt = command.RequestedAt,
            SubmittingChannel = command.SubmittingChannel,
            BatchId = command.BatchId,
            RecordId = command.RecordId,
            BillingComplete = false,
            CustomerUpdateComplete = false
        }, cancellationToken).ConfigureAwait(false);

        await _messageSession.SendLocal(command, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "IssuePolicy dispatched — issuanceId={IssuanceId}",
            command.IssuanceId);

        return Accepted(new
        {
            issuanceId = command.IssuanceId,
            status = "Initiated",
            message = "Policy issuance workflow started. Use issuanceId to track progress."
        });
    }

    [HttpGet("issue/{issuanceId}")]
    public async Task<IActionResult> GetSagaState(string issuanceId, CancellationToken cancellationToken)
    {
        var record = await _repository.GetAsync(issuanceId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            _logger.LogWarning(
                "Saga not found — issuanceId={IssuanceId}",
                issuanceId);
            return NotFound();
        }

        return Ok(record);
    }
}

public sealed class PolicyIssuanceRequest
{
    public string? IssuanceId { get; set; }
    public string? AccountId { get; set; }
    public List<PolicyIssuancePolicyItemRequest> Policies { get; set; } = [];
    public string? SubmittingChannel { get; set; }
    public DateTimeOffset? RequestedAt { get; set; }
    public string? BatchId { get; set; }
    public string? RecordId { get; set; }
}

public sealed class PolicyIssuancePolicyItemRequest
{
    public int PolicyTypeCode { get; set; }
    public int PolicyTypeSubCode { get; set; }
    public Dictionary<string, object?> PolicyData { get; set; } = [];
}
