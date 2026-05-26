namespace dotnet_policy_issuance.Infrastructure;

public static class PolicyIssuanceRuntime
{
    public static IIssuanceSagaRecordRepository Repository { get; set; } = new NullIssuanceSagaRecordRepository();
}
