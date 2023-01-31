using System.Diagnostics;
using IB.WatchCluster.Abstract;
using IB.WatchCluster.Abstract.Configuration;
using IB.WatchCluster.Abstract.Entity.Configuration;
using IB.WatchCluster.Abstract.Kafka;
using IB.WatchCluster.Abstract.Services;
using IB.WatchCluster.YasTelegramBot.Configuration;
using IB.WatchCluster.YasTelegramBot.Infrastructure;
using IB.WatchCluster.YasTelegramBot.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Exceptions;
using Telegram.Bot;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information($"Starting YAS Bot");

await Host.CreateDefaultBuilder(args)
    .ConfigureLogging((context, logBuilder) =>
    {
        var logger = new LoggerConfiguration()
          .ReadFrom.Configuration(context.Configuration)
          .Enrich.FromLogContext()
          .Enrich.WithExceptionDetails()
          .Enrich.WithTraceIdentifier()
          .Enrich.WithProperty("version", SolutionInfo.Version)
          .Enrich.WithProperty("Application", SolutionInfo.Name)
          .CreateLogger();
        
        Log.Logger = logger;
        logBuilder.ClearProviders();
        logBuilder.AddSerilog(logger);
    })
    .ConfigureServices((hostContext, services) =>
    {
        var appConfig = hostContext.Configuration.LoadVerifiedConfiguration<BotConfiguration>();
        var healthcheckConfig = hostContext.Configuration.LoadVerifiedConfiguration<HealthcheckConfig>();
        var kafkaConfig = hostContext.Configuration.LoadVerifiedConfiguration<KafkaConfiguration>();
        var yasBotHandler = new YasBotServiceHandler();
        var otelMetrics = new OtelMetrics("yasbot");

        services.AddOpenTelemetryTracing(builder => builder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(SolutionInfo.Name))
            .AddSource(SolutionInfo.Name)
            .AddOtlpExporter(options => options.Endpoint = new Uri(appConfig.OpenTelemetryCollectorUrl)));

        services.AddOpenTelemetryMetrics(builder => builder
             .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(otelMetrics.MetricJob))
             .AddMeter(otelMetrics.MetricJob)
             .AddOtlpExporter(options => options.Endpoint = new Uri(appConfig.OpenTelemetryCollectorUrl)));

        services.AddSingleton(kafkaConfig);
        services.AddSingleton<IKafkaBroker, KafkaBroker>();
        services.AddSingleton<YasManager>();
        services.AddSingleton(appConfig);
        services.AddSingleton(otelMetrics);
        services.AddSingleton(new ActivitySource(SolutionInfo.Name));
        services.AddSingleton(healthcheckConfig);
        services.AddSingleton(yasBotHandler);

        services.AddSingleton(new TelegramBotClient(appConfig.BotApiKey));
        services.AddHttpClient<YasUpdateHandler>(config => config.BaseAddress = new Uri(appConfig.BaseStorageApiUrl));
        services.AddHttpClient<YasHttpClient>(config => config.BaseAddress = new Uri(appConfig.BaseReaderApiUrl));

        services.AddHostedService<YasBotService>();
        
        // healthcheck
        //
        services
            .AddHealthChecks()
            .AddCheck(
                "self",
                () => yasBotHandler.IsRunning ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy(),
                new[] { healthcheckConfig.LiveFilterTag });
        services.AddHostedService<HealthcheckPublisherService>();
    })
    .RunConsoleAsync();