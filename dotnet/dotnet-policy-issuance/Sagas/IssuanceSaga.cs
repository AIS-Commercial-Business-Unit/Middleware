using dotnet_policy_issuance.Domain;
using dotnet_policy_issuance.Infrastructure;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;

namespace dotnet_policy_issuance.Sagas;

public sealed class IssuanceSaga : Saga<IssuanceSagaData>,
    IAmStartedByMessages<IssuePolicyCommand>,
    IHandleMessages<ComplianceClearedEvent>,
    IHandleMessages<ComplianceBlockedEvent>,
    IHandleMessages<AccountServiceRecordRetrievedEvent>,
    IHandleMessages<PolicyAdminSystemResponseReceivedEvent>,
    IHandleMessages<PolicyAdminSystemCallFailedEvent>,
    IHandleMessages<BillingAssociationCreatedEvent>,
    IHandleMessages<CustomerUpdatedEvent>
{
    private readonly IIssuanceSagaRecordRepository _repository;

    public IssuanceSaga(IIssuanceSagaRecordRepository repository)
    {
        _repository = repository;
    }

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<IssuanceSagaData> mapper)
    {
        mapper.MapSaga(data => data.IssuanceId)
            .ToMessage<IssuePolicyCommand>(message => message.IssuanceId)
            .ToMessage<ComplianceClearedEvent>(message => message.IssuanceId)
            .ToMessage<ComplianceBlockedEvent>(message => message.IssuanceId)
            .ToMessage<AccountServiceRecordRetrievedEvent>(message => message.IssuanceId)
            .ToMessage<PolicyAdminSystemResponseReceivedEvent>(message => message.IssuanceId)
            .ToMessage<PolicyAdminSystemCallFailedEvent>(message => message.IssuanceId)
            .ToMessage<BillingAssociationCreatedEvent>(message => message.IssuanceId)
            .ToMessage<CustomerUpdatedEvent>(message => message.IssuanceId);
    }

    public async Task Handle(IssuePolicyCommand message, IMessageHandlerContext context)
    {
        if (ShouldTriggerServicePulseRetryDemo(message.AccountId))
            throw new InvalidOperationException(
                $"[DEMO] AccountId '{message.AccountId}' is a ServicePulse retry demo trigger. " +
                "In ServicePulse, edit AccountId to a valid value (e.g. ACC-RETRY-001) and retry — the saga will continue normally.");

        Data ??= new IssuanceSagaData();
        var firstPolicy = message.Policies.First();
        Data.IssuanceId = message.IssuanceId;
        Data.AccountId = message.AccountId;
        Data.PolicyTypeCode = firstPolicy.PolicyTypeCode;
        Data.PolicyTypeSubCode = firstPolicy.PolicyTypeSubCode;
        Data.SubmittingChannel = message.SubmittingChannel;
        Data.RequestedAt = message.RequestedAt;
        Data.BatchId = message.BatchId;
        Data.RecordId = message.RecordId;
        Data.Status = "AwaitingCompliance";

        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
        using (LogContext.PushProperty("accountId", Data.AccountId))
        {
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "IssuanceSaga STARTED — issuanceId={IssuanceId} accountId={AccountId} policyTypeCode={PolicyTypeCode} channel={Channel} → AwaitingCompliance",
                Data.IssuanceId, Data.AccountId, Data.PolicyTypeCode, Data.SubmittingChannel);
        }

        await PersistAsync().ConfigureAwait(false);

        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
        {
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "[EDA publish] dotnet-policy-issuance publishing IssuanceSagaStartedEvent — issuanceId={IssuanceId}",
                Data.IssuanceId);
        }

        await context.Publish(new IssuanceSagaStartedEvent
        {
            IssuanceId = Data.IssuanceId,
            AccountId = Data.AccountId,
            StartedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);

        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
        {
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "[EDA publish] dotnet-policy-issuance publishing PolicyIssuanceInitiatedEvent — issuanceId={IssuanceId}",
                Data.IssuanceId);
        }

        await context.Publish(new PolicyIssuanceInitiatedEvent
        {
            IssuanceId = Data.IssuanceId,
            AccountId = Data.AccountId,
            PolicyTypeCode = Data.PolicyTypeCode,
            RequestedAt = Data.RequestedAt
        }).ConfigureAwait(false);
    }

    public async Task Handle(ComplianceClearedEvent message, IMessageHandlerContext context)
    {
        Data.Status = "AwaitingAccountRecord";
        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
        {
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "[EDA subscriber] dotnet-policy-issuance received ComplianceClearedEvent — issuanceId={IssuanceId}",
                Data.IssuanceId);
        }

        await PersistAsync().ConfigureAwait(false);

        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
        {
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "[EDA publish] dotnet-policy-issuance publishing AccountLookupRequestedEvent — issuanceId={IssuanceId}",
                Data.IssuanceId);
        }

        await context.Publish(new AccountLookupRequestedEvent
        {
            IssuanceId = Data.IssuanceId,
            AccountId = Data.AccountId,
            RequestedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);
    }

    public async Task Handle(ComplianceBlockedEvent message, IMessageHandlerContext context)
    {
        Data.Status = "ComplianceBlocked";
        Data.FailureReason = message.Reason;
        Data.CompletedAt = message.BlockedAt == default ? DateTimeOffset.UtcNow : message.BlockedAt;
        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
            PolicyIssuanceRuntime.Logger?.LogWarning(
                "IssuanceSaga ComplianceBlocked — issuanceId={IssuanceId} reason={Reason}",
                Data.IssuanceId, message.Reason);
        await PersistAsync().ConfigureAwait(false);

        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
        {
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "[EDA publish] dotnet-policy-issuance publishing IssuanceFailedEvent — issuanceId={IssuanceId}",
                Data.IssuanceId);
        }

        await context.Publish(new IssuanceFailedEvent
        {
            IssuanceId = Data.IssuanceId,
            AccountId = Data.AccountId,
            Reason = message.Reason,
            FailedAt = Data.CompletedAt.Value,
            BatchId = Data.BatchId,
            RecordId = Data.RecordId
        }).ConfigureAwait(false);

        await SendNotificationAsync(context, "ComplianceBlocked", message.Reason).ConfigureAwait(false);
        MarkAsComplete();
    }

    public async Task Handle(AccountServiceRecordRetrievedEvent message, IMessageHandlerContext context)
    {
        Data.AccountServiceRequestNumber = message.AccountServiceRequestNumber;
        Data.Status = "AwaitingPAS";
        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
        {
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "[EDA subscriber] dotnet-policy-issuance received AccountServiceRecordRetrievedEvent — issuanceId={IssuanceId}",
                Data.IssuanceId);
        }

        await PersistAsync().ConfigureAwait(false);

        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
        {
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "[EDA publish] dotnet-policy-issuance publishing IssuePolicyRequestedEvent — issuanceId={IssuanceId}",
                Data.IssuanceId);
        }

        await context.Publish(new IssuePolicyRequestedEvent
        {
            IssuanceId = Data.IssuanceId,
            AccountId = Data.AccountId,
            PolicyTypeCode = Data.PolicyTypeCode,
            PolicyTypeSubCode = Data.PolicyTypeSubCode,
            AccountServiceRequestNumber = Data.AccountServiceRequestNumber ?? string.Empty,
            RequestedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);
    }

    public async Task Handle(PolicyAdminSystemResponseReceivedEvent message, IMessageHandlerContext context)
    {
        Data.TargetPas = message.TargetPas;
        Data.PolicyNumbers = message.PolicyNumbers;
        Data.Status = "PASConfirmed";
        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
        {
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "[EDA subscriber] dotnet-policy-issuance received PolicyAdminSystemResponseReceivedEvent — issuanceId={IssuanceId}",
                Data.IssuanceId);
        }

        await PersistAsync().ConfigureAwait(false);

        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
        {
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "PASConfirmed — issuanceId={IssuanceId} targetPas={TargetPas} [EDA fan-out: billing-finance + customer-identity subscribed to PolicyAdminSystemResponseReceived]",
                Data.IssuanceId,
                Data.TargetPas);
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "[EDA fan-out] PolicyAdminSystemResponseReceived published — billing-finance + customer-identity subscribed — issuanceId={IssuanceId}",
                Data.IssuanceId);
        }
    }

    public async Task Handle(PolicyAdminSystemCallFailedEvent message, IMessageHandlerContext context)
    {
        Data.Status = "Failed";
        Data.FailureReason = message.Reason;
        Data.CompletedAt = message.FailedAt == default ? DateTimeOffset.UtcNow : message.FailedAt;
        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
            PolicyIssuanceRuntime.Logger?.LogError(
                "IssuanceSaga PASFailed — issuanceId={IssuanceId} reason={Reason}",
                Data.IssuanceId, message.Reason);
        await PersistAsync().ConfigureAwait(false);

        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
        {
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "[EDA publish] dotnet-policy-issuance publishing IssuanceFailedEvent — issuanceId={IssuanceId}",
                Data.IssuanceId);
        }

        await context.Publish(new IssuanceFailedEvent
        {
            IssuanceId = Data.IssuanceId,
            AccountId = Data.AccountId,
            Reason = message.Reason,
            FailedAt = Data.CompletedAt.Value,
            BatchId = Data.BatchId,
            RecordId = Data.RecordId
        }).ConfigureAwait(false);

        await SendNotificationAsync(context, "IssuanceFailed", message.Reason).ConfigureAwait(false);
        MarkAsComplete();
    }

    public async Task Handle(BillingAssociationCreatedEvent message, IMessageHandlerContext context)
    {
        Data.BillingComplete = true;
        Data.Status = Data.CustomerUpdateComplete ? "Completed" : "BillingAssociated";
        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "IssuanceSaga BillingComplete — issuanceId={IssuanceId} customerDone={CustomerDone} → {Status}",
                Data.IssuanceId, Data.CustomerUpdateComplete, Data.Status);
        await PersistAsync().ConfigureAwait(false);

        if (Data.BillingComplete && Data.CustomerUpdateComplete)
        {
            await CompleteIssuanceAsync(context).ConfigureAwait(false);
        }
    }

    public async Task Handle(CustomerUpdatedEvent message, IMessageHandlerContext context)
    {
        Data.CustomerUpdateComplete = true;
        Data.Status = Data.BillingComplete ? "Completed" : "CustomerUpdateComplete";
        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "IssuanceSaga CustomerUpdateComplete — issuanceId={IssuanceId} billingDone={BillingDone} → {Status}",
                Data.IssuanceId, Data.BillingComplete, Data.Status);
        await PersistAsync().ConfigureAwait(false);

        if (Data.BillingComplete && Data.CustomerUpdateComplete)
        {
            await CompleteIssuanceAsync(context).ConfigureAwait(false);
        }
    }

    private async Task CompleteIssuanceAsync(IMessageHandlerContext context)
    {
        Data.Status = "Completed";
        Data.CompletedAt = DateTimeOffset.UtcNow;
        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "IssuanceSaga COMPLETED — issuanceId={IssuanceId} policyNumbers={PolicyNumbers} durationMs={DurationMs}",
                Data.IssuanceId,
                string.Join(",", Data.PolicyNumbers),
                (long)(Data.CompletedAt.Value - Data.RequestedAt).TotalMilliseconds);
        await PersistAsync().ConfigureAwait(false);

        using (LogContext.PushProperty("issuanceId", Data.IssuanceId))
        {
            PolicyIssuanceRuntime.Logger?.LogInformation(
                "[EDA publish] dotnet-policy-issuance publishing PolicyIssuedEvent — issuanceId={IssuanceId}",
                Data.IssuanceId);
        }

        await context.Publish(new PolicyIssuedEvent
        {
            IssuanceId = Data.IssuanceId,
            AccountId = Data.AccountId,
            PolicyNumbers = Data.PolicyNumbers,
            CompletedAt = Data.CompletedAt.Value,
            BatchId = Data.BatchId,
            RecordId = Data.RecordId
        }).ConfigureAwait(false);

        await SendNotificationAsync(context, "PolicyIssued", "Policy issuance completed successfully.").ConfigureAwait(false);
        MarkAsComplete();
    }

    private async Task SendNotificationAsync(IMessageHandlerContext context, string type, string message)
    {
        await context.Send(new PublishNotificationIntentCommand
        {
            IssuanceId = Data.IssuanceId,
            AccountId = Data.AccountId,
            NotificationType = type,
            Message = message
        }).ConfigureAwait(false);
    }

    private static bool ShouldTriggerServicePulseRetryDemo(string accountId) =>
        string.Equals(accountId, "FAIL-SERVICEPULSE-001", StringComparison.OrdinalIgnoreCase);

    private Task PersistAsync()
    {
        return _repository.UpsertAsync(new IssuanceSagaRecord
        {
            IssuanceId = Data.IssuanceId,
            AccountId = Data.AccountId,
            AccountServiceRequestNumber = Data.AccountServiceRequestNumber,
            PolicyNumbers = Data.PolicyNumbers.Count == 0 ? null : [.. Data.PolicyNumbers],
            Status = Data.Status,
            PolicyTypeCode = Data.PolicyTypeCode,
            PolicyTypeSubCode = Data.PolicyTypeSubCode,
            TargetPas = Data.TargetPas,
            PasRetryCount = Data.PasRetryCount,
            BillingComplete = Data.BillingComplete,
            CustomerUpdateComplete = Data.CustomerUpdateComplete,
            FailureReason = Data.FailureReason,
            RequestedAt = Data.RequestedAt,
            CompletedAt = Data.CompletedAt,
            SubmittingChannel = Data.SubmittingChannel,
            BatchId = Data.BatchId,
            RecordId = Data.RecordId
        });
    }
}