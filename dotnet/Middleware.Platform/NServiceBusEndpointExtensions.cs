namespace Middleware.Platform;

using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;

public static class NServiceBusEndpointExtensions
{
    /// <summary>
    /// Registers Azure AD (Entra ID) authentication providers for Microsoft.Data.SqlClient.
    /// Required because Microsoft.Data.SqlClient 6+ no longer bundles ActiveDirectory* providers;
    /// they now live in Microsoft.Data.SqlClient.Extensions.Azure and must be registered explicitly.
    /// </summary>
    public static IServiceCollection AddAzureSqlAuthentication(this IServiceCollection services)
    {
        services.AddAzureClients(builder =>
        {
            builder.AddSqlClient();
            builder.UseCredential(new DefaultAzureCredential());
        });
        return services;
    }
    /// <summary>
    /// Applies standard Particular Service Platform defaults to an endpoint:
    /// error queue, message audit, heartbeats, custom checks, metrics, and
    /// optionally saga audit for endpoints that host sagas.
    ///
    /// Queue names default to Particular conventions and can be overridden via:
    ///   NServiceBus:ServicePlatform:ErrorQueue
    ///   NServiceBus:ServicePlatform:AuditQueue
    ///   NServiceBus:ServicePlatform:ServiceControlQueue
    ///   NServiceBus:ServicePlatform:MonitoringQueue
    /// </summary>
    public static void ApplyParticularPlatformDefaults(
        this EndpointConfiguration endpointConfiguration,
        IConfiguration configuration,
        bool withSagaAudit = false)
    {
        var errorQueue = configuration["NServiceBus:ServicePlatform:ErrorQueue"] ?? "error";
        var auditQueue = configuration["NServiceBus:ServicePlatform:AuditQueue"] ?? "audit";
        var serviceControlQueue = configuration["NServiceBus:ServicePlatform:ServiceControlQueue"] ?? "Particular.ServiceControl";
        var monitoringQueue = configuration["NServiceBus:ServicePlatform:MonitoringQueue"] ?? "Particular.Monitoring";

        endpointConfiguration.ConnectToServicePlatform(new ServicePlatformConnectionConfiguration
        {
            ErrorQueue = errorQueue,
            Heartbeats = new ServicePlatformHeartbeatConfiguration
            {
                Enabled = true,
                HeartbeatsQueue = serviceControlQueue
            },
            CustomChecks = new ServicePlatformCustomChecksConfiguration
            {
                Enabled = true,
                CustomChecksQueue = serviceControlQueue
            },
            MessageAudit = new ServicePlatformMessageAuditConfiguration
            {
                Enabled = true,
                AuditQueue = auditQueue
            },
            Metrics = new ServicePlatformMetricsConfiguration
            {
                Enabled = true,
                MetricsQueue = monitoringQueue,
                Interval = TimeSpan.FromSeconds(30)
            },
            SagaAudit = new ServicePlatformSagaAuditConfiguration
            {
                Enabled = withSagaAudit,
                SagaAuditQueue = auditQueue
            }
        });
    }
}
