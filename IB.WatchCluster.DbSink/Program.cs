using Confluent.Kafka;
using IB.WatchCluster.Abstract;
using IB.WatchCluster.Abstract.Configuration;
using IB.WatchCluster.Abstract.Entity.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using System.Diagnostics;
using IB.WatchCluster.Abstract.Kafka;
using IB.WatchCluster.Abstract.Services;
using OpenTelemetry.Metrics;
using IB.WatchCluster.DbSink.Configuration;
using IB.WatchCluster.DbSink.Infrastructure;
using IB.WatchCluster.DbSink;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog.Exceptions;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information($"Starting the Service");

await Host.CreateDefaultBuilder(args)
    .ConfigureLogging((context, logBuilder) =>
    {
        var logger = new LoggerConfiguration()
          .ReadFrom.Configuration(context.Configuration)
          .Enrich.FromLogContext()
          .Enrich.WithExceptionDetails()
          .Enrich.WithProperty("version", SolutionInfo.Version)
          .Enrich.WithProperty("Application", SolutionInfo.Name)
          .CreateLogger();
        Log.Logger = logger;

        logBuilder.ClearProviders();
        logBuilder.AddSerilog(logger);
    })
    .ConfigureServices((hostContext, services) =>
    {
        var appConfig = hostContext.Configuration.LoadVerifiedConfiguration<DbSinkConfiguration>();
        var healthcheckConfig = hostContext.Configuration.LoadVerifiedConfiguration<HealthcheckConfig>();
        var kafkaConfig = hostContext.Configuration.LoadVerifiedConfiguration<KafkaConfiguration>();
        kafkaConfig.SetDefaults("dbsink-postgres");
        var pgConfig = hostContext.Configuration.LoadVerifiedConfiguration<PgProviderConfiguration>();
        var sinkServiceHandler = new SinkServiceHandler();

        services.AddOpenTelemetryTracing(builder => builder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(SolutionInfo.Name))
            .AddSource(SolutionInfo.Name)
            .AddOtlpExporter(options => options.Endpoint = new Uri(appConfig.OpenTelemetryCollectorUrl)));

        services.AddOpenTelemetryMetrics(builder => builder
             .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(OtelMetrics.MetricName))
             .AddMeter(OtelMetrics.MetricName)
             .AddOtlpExporter(options => options.Endpoint = new Uri(appConfig.OpenTelemetryCollectorUrl)));

        services.AddSingleton(pgConfig.ConnectionFactory());
        services.AddSingleton<OtelMetrics>();
        services.AddSingleton(new ActivitySource(SolutionInfo.Name));
        services.AddSingleton(kafkaConfig);
        services.AddSingleton(healthcheckConfig);
        services.AddSingleton<KafkaBroker>();
        services.AddSingleton(sinkServiceHandler);
        services.AddHostedService<SinkService>();
        
        // healthcheck
        //
        services
            .AddHealthChecks()
            .AddCheck(
                "self",
                () => sinkServiceHandler.IsRunning ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy(),
                new[] { healthcheckConfig.LiveFilterTag })
            .AddKafka(
                kafkaConfig.BuildProducerConfig(), 
                "healthcheck", 
                "kafka", 
                null, 
                new[]{ "readiness" }, 
                TimeSpan.FromSeconds(30))
            .AddNpgSql(pgConfig.BuildConnectionString());
        services.AddHostedService<HealthcheckPublisherService>();

    })
    .RunConsoleAsync();
