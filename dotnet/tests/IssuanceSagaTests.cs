using dotnet_policy_issuance.Domain;
using dotnet_policy_issuance.Infrastructure;
using dotnet_policy_issuance.Sagas;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using Middleware.Contracts.Models;
using NServiceBus.Testing;
using NUnit.Framework;

namespace Middleware.Tests;

[TestFixture]
public sealed class IssuanceSagaTests
{
    [Test]
    public async Task WhenIssuePolicyReceived_SagaPublishesPolicyIssuanceInitiated()
    {
        var saga = new IssuanceSaga(new InMemoryIssuanceSagaRecordRepository());
        var context = new TestableMessageHandlerContext();
        var command = CreateCommand();

        await saga.Handle(command, context);

        Assert.That(saga.Data.Status, Is.EqualTo("AwaitingCompliance"));
        Assert.That(context.PublishedMessages.Any(message => message.Message<PolicyIssuanceInitiatedEvent>() is not null), Is.True);
        Assert.That(context.SentMessages.Any(message => message.Message<RequestComplianceCheckCommand>() is not null), Is.False);
    }

    [Test]
    public async Task WhenComplianceCleared_SagaPublishesAccountLookupRequested()
    {
        var saga = new IssuanceSaga(new InMemoryIssuanceSagaRecordRepository());
        var context = new TestableMessageHandlerContext();
        var command = CreateCommand();

        await saga.Handle(command, context);
        await saga.Handle(new ComplianceClearedEvent
        {
            IssuanceId = command.IssuanceId,
            AccountId = command.AccountId,
            CheckId = "CHK-1",
            ClearedAt = DateTimeOffset.UtcNow
        }, context);

        Assert.That(saga.Data.Status, Is.EqualTo("AwaitingAccountRecord"));
        Assert.That(context.PublishedMessages.Any(message => message.Message<AccountLookupRequestedEvent>() is not null), Is.True);
        Assert.That(context.SentMessages.Any(message => message.Message<GetOrCreateAccountServiceRecordCommand>() is not null), Is.False);
    }

    [Test]
    public async Task WhenAccountRecordRetrieved_SagaPublishesIssuePolicyRequestedWithoutPasCommand()
    {
        var saga = new IssuanceSaga(new InMemoryIssuanceSagaRecordRepository());
        var context = new TestableMessageHandlerContext();
        var command = CreateCommand();

        await saga.Handle(command, context);
        await saga.Handle(new AccountServiceRecordRetrievedEvent
        {
            IssuanceId = command.IssuanceId,
            AccountId = command.AccountId,
            AccountServiceRequestNumber = "ASR-123",
            RetrievedAt = DateTimeOffset.UtcNow
        }, context);

        Assert.That(saga.Data.Status, Is.EqualTo("AwaitingPAS"));
        var issuePolicyRequested = context.PublishedMessages
            .Select(message => message.Message<IssuePolicyRequestedEvent>())
            .LastOrDefault(message => message is not null);
        Assert.That(issuePolicyRequested, Is.Not.Null);
        Assert.That(issuePolicyRequested!.AccountServiceRequestNumber, Is.EqualTo("ASR-123"));
        Assert.That(context.SentMessages.Any(message => message.Message<IssueToAdminSystemCommand>() is not null), Is.False);
    }

    [Test]
    public async Task WhenPasResponseReceived_SagaWaitsForFanOutSubscribersWithoutCommands()
    {
        var saga = new IssuanceSaga(new InMemoryIssuanceSagaRecordRepository());
        var context = new TestableMessageHandlerContext();
        var command = CreateCommand();

        await saga.Handle(command, context);
        await saga.Handle(new AccountServiceRecordRetrievedEvent
        {
            IssuanceId = command.IssuanceId,
            AccountId = command.AccountId,
            AccountServiceRequestNumber = "ASR-123",
            RetrievedAt = DateTimeOffset.UtcNow
        }, context);
        await saga.Handle(new PolicyAdminSystemResponseReceivedEvent
        {
            IssuanceId = command.IssuanceId,
            AccountId = command.AccountId,
            TargetPas = "DuckCreek-Commercial",
            AccountServiceRequestNumber = "ASR-123",
            PolicyNumbers = ["POL-1001"],
            ReceivedAt = DateTimeOffset.UtcNow
        }, context);

        Assert.That(saga.Data.Status, Is.EqualTo("PASConfirmed"));
        Assert.That(context.SentMessages.Any(message => message.Message<AssociateBillingAccountCommand>() is not null), Is.False);
        Assert.That(context.SentMessages.Any(message => message.Message<UpdateCustomerRecordCommand>() is not null), Is.False);
    }

    [Test]
    public async Task WhenComplianceBlocked_SagaTransitionsToComplianceBlocked()
    {
        var saga = new IssuanceSaga(new InMemoryIssuanceSagaRecordRepository());
        var context = new TestableMessageHandlerContext();
        var command = CreateCommand();

        await saga.Handle(command, context);
        await saga.Handle(new ComplianceBlockedEvent
        {
            IssuanceId = command.IssuanceId,
            AccountId = command.AccountId,
            CheckId = "CHK-2",
            Reason = "Sanctions match",
            BlockedAt = DateTimeOffset.UtcNow
        }, context);

        Assert.That(saga.Data.Status, Is.EqualTo("ComplianceBlocked"));
        Assert.That(context.PublishedMessages.Any(message => message.Message<IssuanceFailedEvent>() is not null), Is.True);
    }

    [Test]
    public async Task WhenBothBillingAndCustomerComplete_SagaCompletes()
    {
        var saga = new IssuanceSaga(new InMemoryIssuanceSagaRecordRepository());
        var context = new TestableMessageHandlerContext();
        var command = CreateCommand();

        await saga.Handle(command, context);
        await saga.Handle(new ComplianceClearedEvent
        {
            IssuanceId = command.IssuanceId,
            AccountId = command.AccountId,
            CheckId = "CHK-3",
            ClearedAt = DateTimeOffset.UtcNow
        }, context);
        await saga.Handle(new AccountServiceRecordRetrievedEvent
        {
            IssuanceId = command.IssuanceId,
            AccountId = command.AccountId,
            AccountServiceRequestNumber = "ASR-123",
            RetrievedAt = DateTimeOffset.UtcNow
        }, context);
        await saga.Handle(new PolicyAdminSystemResponseReceivedEvent
        {
            IssuanceId = command.IssuanceId,
            AccountId = command.AccountId,
            TargetPas = "DuckCreek-Commercial",
            AccountServiceRequestNumber = "ASR-123",
            PolicyNumbers = ["POL-1001"],
            ReceivedAt = DateTimeOffset.UtcNow
        }, context);
        await saga.Handle(new BillingAssociationCreatedEvent
        {
            IssuanceId = command.IssuanceId,
            AccountId = command.AccountId,
            AccountServiceRequestNumber = "ASR-123",
            BillingChannel = "DirectBill",
            CreatedAt = DateTimeOffset.UtcNow
        }, context);
        await saga.Handle(new CustomerUpdatedEvent
        {
            IssuanceId = command.IssuanceId,
            AccountId = command.AccountId,
            FieldsUpdated = ["policyNumbers"],
            UpdatedAt = DateTimeOffset.UtcNow
        }, context);

        Assert.That(saga.Data.Status, Is.EqualTo("Completed"));
        Assert.That(context.PublishedMessages.Any(message => message.Message<PolicyIssuedEvent>() is not null), Is.True);
    }

    [Test]
    public void WhenDemoMagicAccountId_ThrowsBeforeAnySagaStateIsWritten()
    {
        var repo = new InMemoryIssuanceSagaRecordRepository();
        var saga = new IssuanceSaga(repo);
        var context = new TestableMessageHandlerContext();
        var command = CreateCommand();
        command.AccountId = "FAIL-SERVICEPULSE-001";

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => saga.Handle(command, context));

        Assert.That(ex!.Message, Does.Contain("[DEMO]"));
        Assert.That(ex.Message, Does.Contain("FAIL-SERVICEPULSE-001"));
        Assert.That(context.PublishedMessages, Is.Empty, "No events should be published before the guard throws");
        Assert.That(saga.Data, Is.Null.Or.EqualTo(new IssuanceSagaData()), "Saga state must not be committed");
    }

    [Test]
    public async Task WhenDemoAccountIdCorrectedAndRetried_SagaProceedsNormally()
    {
        // Simulates the ServicePulse retry: same IssuanceId, AccountId corrected to a valid value.
        var saga = new IssuanceSaga(new InMemoryIssuanceSagaRecordRepository());
        var context = new TestableMessageHandlerContext();
        var command = CreateCommand();
        command.AccountId = "ACC-RETRY-001";

        await saga.Handle(command, context);

        Assert.That(saga.Data.Status, Is.EqualTo("AwaitingCompliance"));
        Assert.That(context.PublishedMessages.Any(m => m.Message<PolicyIssuanceInitiatedEvent>() is not null), Is.True);
    }

    private static IssuePolicyCommand CreateCommand()
    {
        return new IssuePolicyCommand
        {
            IssuanceId = Guid.NewGuid().ToString(),
            AccountId = "ACC-UNITTEST",
            Policies = [new PolicyItem { PolicyTypeCode = 1, PolicyTypeSubCode = 0 }],
            RequestedAt = DateTimeOffset.UtcNow,
            SubmittingChannel = "DirectRequest"
        };
    }
}

internal sealed class InMemoryIssuanceSagaRecordRepository : IIssuanceSagaRecordRepository
{
    private readonly Dictionary<string, IssuanceSagaRecord> _store = [];

    public Task<IssuanceSagaRecord?> GetAsync(string issuanceId, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(issuanceId, out var record);
        return Task.FromResult(record);
    }

    public Task UpsertAsync(IssuanceSagaRecord record, CancellationToken cancellationToken = default)
    {
        _store[record.IssuanceId] = record;
        return Task.CompletedTask;
    }
}
