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
    private readonly HealthcheckConfig _healthcheckConfig;
    private readonly HealthCheckService _healthCheckService;

    public HealthcheckPublisherService(
        ILogger<HealthcheckPublisherService> logger, HealthcheckConfig healthcheckConfig, HealthCheckService healthCheckService)
    {
        _logger = logger;
        _healthcheckConfig = healthcheckConfig;
        _healthCheckService = healthCheckService;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_healthcheckConfig.LiveProbeUrl) &&
            string.IsNullOrWhiteSpace(_healthcheckConfig.ReadyProbeUrl))
        {
            _logger.LogWarning("Could not start health check publisher, as no healthcheck endpoints defined");
            return;
        }
            
        using var listener = new HttpListener();

        if (!string.IsNullOrWhiteSpace(_healthcheckConfig.LiveProbeUrl))
        {
            listener.Prefixes.Add($"http://*:{_healthcheckConfig.HttpPort}{_healthcheckConfig.LiveProbeUrl}");
            _logger.LogInformation(
                "Healthcheck Live probe listens on port {@port}, url {@url}", 
                _healthcheckConfig.HttpPort, 
                _healthcheckConfig.LiveProbeUrl);
        }

        if (!string.IsNullOrWhiteSpace(_healthcheckConfig.ReadyProbeUrl))
        {
            listener.Prefixes.Add($"http://*:{_healthcheckConfig.HttpPort}{_healthcheckConfig.ReadyProbeUrl}");           
            _logger.LogInformation(
                "Healthcheck Ready probe listens on port {@port}, url {@url}", 
                _healthcheckConfig.HttpPort, 
                _healthcheckConfig.ReadyProbeUrl);
        }

        listener.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);

                Func<HealthCheckRegistration, bool> predicate = null;
                if (!string.IsNullOrWhiteSpace(_healthcheckConfig.LiveProbeUrl) &&
                    (context.Request.RawUrl?.StartsWith(_healthcheckConfig.LiveProbeUrl.TrimEnd('/')) ?? false))
                {
                    predicate = r => r.Tags.Contains(_healthcheckConfig.LiveFilterTag);
                }

                var healthReport = await _healthCheckService.CheckHealthAsync(predicate, stoppingToken);
                await HealthcheckStatic.HealthResultResponseJsonFull(context, healthReport).ConfigureAwait(false);
            }
            catch (HttpListenerException e)
            {
                _logger.LogCritical(e, "HttpListenerException {@code}, {@msg}", e.ErrorCode, e.Message);
                break;
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