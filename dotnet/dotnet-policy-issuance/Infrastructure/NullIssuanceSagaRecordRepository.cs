using dotnet_policy_issuance.Domain;

namespace dotnet_policy_issuance.Infrastructure;

public sealed class NullIssuanceSagaRecordRepository : IIssuanceSagaRecordRepository
{
    public Task<IssuanceSagaRecord?> GetAsync(string issuanceId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IssuanceSagaRecord?>(null);

    public Task UpsertAsync(IssuanceSagaRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
