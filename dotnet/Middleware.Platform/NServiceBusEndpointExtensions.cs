namespace Middleware.Platform;

using Microsoft.Extensions.Configuration;
using NServiceBus;

public static class NServiceBusEndpointExtensions
{
    /// <summary>
    /// Placeholder for Azure AD (Entra ID) SQL authentication registration.
    /// Microsoft.Data.SqlClient 6+ resolves Azure AD credentials automatically via
    /// DefaultAzureCredential when no password is present in the connection string.
    /// No explicit provider registration is required for local or managed-identity deployments.
    /// </summary>
    public static IServiceCollection AddAzureSqlAuthentication(this IServiceCollection services)
        => services;
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
                Interval = TimeSpan.FromSeconds(5)
            },
            SagaAudit = new ServicePlatformSagaAuditConfiguration
            {
                Enabled = withSagaAudit,
                SagaAuditQueue = auditQueue
            }
        });
    }
}
