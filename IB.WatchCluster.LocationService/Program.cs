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
using IB.WatchCluster.LocationService.Infrastructure;
using IB.WatchCluster.LocationService.Entity;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting the Service");

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
        var appConfig = hostContext.Configuration.LoadVerifiedConfiguration<AppConfiguration>();
        var kafkaConfig = hostContext.Configuration.LoadVerifiedConfiguration<KafkaConfiguration>();
        var consumerConfig = kafkaConfig.BuildConsumerConfig();
        consumerConfig.AutoOffsetReset = AutoOffsetReset.Latest;

        services.AddOpenTelemetryTracing(builder => builder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(SolutionInfo.Name))
            .AddHttpClientInstrumentation()
            .AddSource(SolutionInfo.Name)
            .AddOtlpExporter(options => options.Endpoint = new Uri(appConfig.OpenTelemetryCollectorUrl)));

        services.AddSingleton(new ActivitySource(SolutionInfo.Name));
        services.AddSingleton(kafkaConfig);
        services.AddSingleton<KafkaProducer<string, string>>();
        services.AddSingleton(new ConsumerBuilder<string, string>(consumerConfig).Build());

        services.AddHostedService<ProcessingService>();
    })
    .RunConsoleAsync();

