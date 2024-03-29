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
            string requestUrl = null;
            try
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);
                requestUrl = context.Request.RawUrl;
                using var response = context.Response;
                if (!string.IsNullOrWhiteSpace(_healthcheckConfig.LiveProbeUrl) &&
                    (requestUrl?.StartsWith(_healthcheckConfig.LiveProbeUrl.TrimEnd('/')) ?? false))
                {
                    bool Predicate(HealthCheckRegistration r) => r.Tags.Contains(_healthcheckConfig.LiveFilterTag);
                    var healthReport = await _healthCheckService.CheckHealthAsync(Predicate, stoppingToken);
                    await HealthcheckWriter.HealthResultResponseTextStatus(response, healthReport);
                }
                else
                {
                    var healthReport = await _healthCheckService.CheckHealthAsync(stoppingToken);
                    await HealthcheckWriter.HealthResultResponseJsonFull(response, healthReport).ConfigureAwait(false);
                }
            }
            
            // Ignore writing to broken pipe error as it happened because of connection closed from receiver
            //
            catch (HttpListenerException e) when (e.ErrorCode == -2146232800)
            {
                _logger.LogDebug(e,"Error stream writing on {@url}", requestUrl);
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