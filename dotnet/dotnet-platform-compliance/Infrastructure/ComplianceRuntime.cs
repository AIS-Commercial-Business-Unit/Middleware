using Microsoft.Extensions.Logging;

namespace dotnet_platform_compliance.Infrastructure;

public static class ComplianceRuntime
{
    public static HttpClient HttpClient { get; } = new();
    public static string ComplianceCheckUrl { get; set; } = "http://rsk3x3-compliance-stub:9004/api/check";
    public static ILogger? Logger { get; set; }
}
