using Microsoft.Extensions.Logging;

namespace dotnet_policy_issuance.Infrastructure;

public static class PolicyIssuanceRuntime
{
    public static IIssuanceSagaRecordRepository Repository { get; set; } = new NullIssuanceSagaRecordRepository();
    public static ILogger? Logger { get; set; }
}
