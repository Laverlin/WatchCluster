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
using OpenTelemetry.Metrics;
using IB.WatchCluster.DbSink.Configuration;
using IB.WatchCluster.DbSink.Infrastructure;
using IB.WatchCluster.DbSink;

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
        var kafkaConfig = hostContext.Configuration.LoadVerifiedConfiguration<KafkaConfiguration>();
        kafkaConfig.SetDefaults("dbsink-postgres");
        var consumerConfig = kafkaConfig.BuildConsumerConfig();

        services.AddOpenTelemetryTracing(builder => builder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(SolutionInfo.Name))
            .AddSource(SolutionInfo.Name)
            .AddOtlpExporter(options => options.Endpoint = new Uri(appConfig.OpenTelemetryCollectorUrl)));

        services.AddOpenTelemetryMetrics(builder => builder
             .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(OtelMetrics.MetricName))
             .AddMeter(OtelMetrics.MetricName)
             .AddOtlpExporter(options => options.Endpoint = new Uri(appConfig.OpenTelemetryCollectorUrl)));

        services.AddSingleton(hostContext.Configuration
            .LoadVerifiedConfiguration<PgProviderConfiguration>()
            .ConnectionFactory());
        services.AddSingleton<OtelMetrics>();
        services.AddSingleton(new ActivitySource(SolutionInfo.Name));
        services.AddSingleton(kafkaConfig);
        services.AddSingleton(new ConsumerBuilder<string, string>(consumerConfig).Build());
        services.AddHostedService<SinkService>();

    })
    .RunConsoleAsync();
