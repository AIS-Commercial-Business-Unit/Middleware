using Microsoft.Extensions.Logging;

namespace dotnet_billing_finance.Infrastructure;

public static class BillingRuntime
{
    public static HttpClient HttpClient { get; } = new();
    public static string BillingUrl { get; set; } = "http://crm19x1-billing-stub:9007/api/billing";
    public static ILogger? Logger { get; set; }
}
