using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IB.WatchCluster.Abstract.Services;

public class HealthcheckPublisherService : BackgroundService
{
    private readonly ILogger<HealthcheckPublisherService> _logger;
    private readonly HealthCheckConfig _healthCheckConfig;
    private readonly HealthCheckService _healthCheckService;

    public HealthcheckPublisherService(
        ILogger<HealthcheckPublisherService> logger, HealthCheckConfig healthCheckConfig, HealthCheckService healthCheckService)
    {
        _logger = logger;
        _healthCheckConfig = healthCheckConfig;
        _healthCheckService = healthCheckService;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_healthCheckConfig.LiveProbeUrl) &&
            string.IsNullOrWhiteSpace(_healthCheckConfig.ReadyProbeUrl))
        {
            _logger.LogWarning("Could not start health check publisher, as no healthcheck endpoints defined");
            return;
        }
            
        using var listener = new HttpListener();

        if (!string.IsNullOrWhiteSpace(_healthCheckConfig.LiveProbeUrl))
        {
            listener.Prefixes.Add($"http://*:{_healthCheckConfig.HttpPort}{_healthCheckConfig.LiveProbeUrl}");
            _logger.LogInformation(
                "Healthcheck Live probe listens on port {@port}, url {@url}", 
                _healthCheckConfig.HttpPort, 
                _healthCheckConfig.LiveProbeUrl);
        }

        if (!string.IsNullOrWhiteSpace(_healthCheckConfig.ReadyProbeUrl))
        {
            listener.Prefixes.Add($"http://*:{_healthCheckConfig.HttpPort}{_healthCheckConfig.ReadyProbeUrl}");           
            _logger.LogInformation(
                "Healthcheck Ready probe listens on port {@port}, url {@url}", 
                _healthCheckConfig.HttpPort, 
                _healthCheckConfig.ReadyProbeUrl);
        }

        listener.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);

                Func<HealthCheckRegistration, bool> predicate = null;
                if (!string.IsNullOrWhiteSpace(_healthCheckConfig.LiveProbeUrl) &&
                    (context.Request.RawUrl?.StartsWith(_healthCheckConfig.LiveProbeUrl) ?? false))
                {
                    predicate = r => r.Tags.Contains(_healthCheckConfig.LiveFilterTag);
                }

                var healthReport = await _healthCheckService.CheckHealthAsync(predicate, stoppingToken);
                await HealthcheckStatic.HealthResultResponseJsonFull(context, healthReport).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Fatal error on HealthCheck publisher {@msg}", e.Message);
                throw;
            }
        }

        listener.Stop();
    }
}