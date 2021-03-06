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
using IB.WatchCluster.ServiceHost.Entity;
using IB.WatchCluster.ServiceHost.Infrastructure;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Services;
using OpenTelemetry.Metrics;

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
        var appConfig = hostContext.Configuration.LoadVerifiedConfiguration<AppConfiguration>();
        var kafkaConfig = hostContext.Configuration.LoadVerifiedConfiguration<KafkaConfiguration>();
        var consumerConfig = kafkaConfig.BuildConsumerConfig($"sh-{appConfig.Handler.ToLower()}");

        services.AddOpenTelemetryTracing(builder => builder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(SolutionInfo.Name))
            .AddHttpClientInstrumentation()
            .AddSource(SolutionInfo.Name)
            .AddOtlpExporter(options => options.Endpoint = new Uri(appConfig.OpenTelemetryCollectorUrl)));

       services.AddOpenTelemetryMetrics(builder => builder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(OtMetrics.MetricName))
            .AddHttpClientInstrumentation()
            .AddMeter(OtMetrics.MetricName)
            .AddOtlpExporter(options => options.Endpoint = new Uri(appConfig.OpenTelemetryCollectorUrl)));

        services.AddSingleton<OtMetrics>();
        services.AddSingleton(new ActivitySource(SolutionInfo.Name));
        services.AddSingleton(kafkaConfig);
        services.AddSingleton<KafkaProducer<string, string>>();
        services.AddSingleton(new ConsumerBuilder<string, string>(consumerConfig).Build());

        switch(appConfig.Handler)
        {
            case nameof(LocationInfo):
                services.AddHttpClient<IRequestHandler<LocationInfo>, VirtualEarthService>();
                services.AddSingleton(hostContext.Configuration.LoadVerifiedConfiguration<VirtualEarthConfiguration>());
                services.AddHostedService<ProcessingService<LocationInfo>>();
                break;
            case nameof(WeatherInfo):
                services.AddHttpClient<IRequestHandler<WeatherInfo>, WeatherService>();
                services.AddSingleton(hostContext.Configuration.LoadVerifiedConfiguration<WeatherConfiguration>());
                services.AddHostedService<ProcessingService<WeatherInfo>>();
                break;
            case nameof(ExchangeRateInfo):
                services.AddHttpClient<IRequestHandler<ExchangeRateInfo>, CurrencyExchangeService>();
                services.AddSingleton(hostContext.Configuration.LoadVerifiedConfiguration<CurrencyExchangeConfiguration>());
                services.AddHostedService<ProcessingService<ExchangeRateInfo>>();
                break;
            default: 
                throw new ArgumentException($"Unknown service type { appConfig.Handler }");
        }
    })
    .RunConsoleAsync();

