using dotnet_policy_issuance.Domain;

namespace dotnet_policy_issuance.Infrastructure;

public interface IIssuanceSagaRecordRepository
{
    Task<IssuanceSagaRecord?> GetAsync(string issuanceId, CancellationToken cancellationToken = default);
    Task UpsertAsync(IssuanceSagaRecord record, CancellationToken cancellationToken = default);
}
