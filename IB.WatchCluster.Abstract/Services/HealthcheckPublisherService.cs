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
    private readonly HealthCheckService _healthCheckService;

    public HealthcheckPublisherService(ILogger<HealthcheckPublisherService> logger, HealthCheckService healthCheckService)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://*:80/health/live/");
        listener.Prefixes.Add("http://*:80/health/ready/");
        
        listener.Start();
        _logger.LogInformation("Healthcheck listens on port {@port}", 80);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ctx = await listener.GetContextAsync().ConfigureAwait(false);

                Func<HealthCheckRegistration, bool> predicate = ctx.Request.RawUrl?.StartsWith("/health/live") ?? false
                    ? hcr => hcr.Tags.Contains("live")
                    : null;
                    
                var healthReport = await _healthCheckService.CheckHealthAsync(predicate, stoppingToken);
                await HealthcheckStatic.HealthResultResponseJsonFull(ctx, healthReport).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "fatal error {@msg}", e.Message);
                throw;
            }
        }
        listener.Stop();
    }
}