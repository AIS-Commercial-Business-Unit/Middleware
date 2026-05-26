using Microsoft.Extensions.Logging;

namespace dotnet_customer_identity.Infrastructure;

public static class CustomerIdentityRuntime
{
    public static HttpClient HttpClient { get; } = new();
    public static string AccountServiceUrl { get; set; } = "http://erm7x1-account-stub:9005/api/account";
    public static string CustomerServiceUrl { get; set; } = "http://crm40x1-customer-stub:9006/api/customer";
    public static ILogger? Logger { get; set; }
}
