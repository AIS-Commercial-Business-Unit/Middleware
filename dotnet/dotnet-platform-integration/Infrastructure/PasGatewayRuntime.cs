using Microsoft.Extensions.Logging;

namespace dotnet_platform_integration.Infrastructure;

public static class PasGatewayRuntime
{
    public static HttpClient HttpClient { get; } = new();
    public static string DuckCreekCommercialUrl { get; set; } = "http://duckcreek-commercial-stub:9001/api/issuance";
    public static string DuckCreekPersonalUrl { get; set; } = "http://duckcreek-personal-stub:9002/api/issuance";
    public static string ForeFrontUrl { get; set; } = "http://forefront-stub:9003/api/issuance";
    public static ILogger? Logger { get; set; }
}
