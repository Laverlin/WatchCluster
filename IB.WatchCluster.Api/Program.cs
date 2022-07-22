using Confluent.Kafka;
using IB.WatchCluster.Abstract;
using IB.WatchCluster.Abstract.Configuration;
using IB.WatchCluster.Abstract.Entity.Configuration;
using IB.WatchCluster.Api.Entity;
using IB.WatchCluster.Api.Entity.Configuration;
using IB.WatchCluster.Api.Infrastructure;
using IB.WatchCluster.Api.Infrastructure.Middleware;
using IB.WatchCluster.Api.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using System.Diagnostics;

// Set Bootstrap logger
//
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();
Log.Information("Starting up");

try
{
    // This is required if the collector doesn't expose an https endpoint. By default, .NET
    // only allows http2 (required for gRPC) to secure endpoints.
    //
    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

    var builder = WebApplication.CreateBuilder(args);

    // logging
    //
    var logger = new LoggerConfiguration()
      .ReadFrom.Configuration(builder.Configuration)
      .Enrich.FromLogContext()
      .Enrich.WithSpan()
      .Enrich.WithProperty("version", SolutionInfo.Version)
      .Enrich.WithProperty("Application", SolutionInfo.Name)
      .CreateLogger();
    Log.Logger = logger;
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(logger);
    builder.Host.UseSerilog(logger);

    // configuration
    //
    builder.Configuration.AddEnvironmentVariables();
    var apiConfiguration = builder.Configuration.LoadVerifiedConfiguration<ApiConfiguration>();
    var kafkaConfig = builder.Configuration.LoadVerifiedConfiguration<KafkaConfiguration>();

    // Metrics & Tracing
    //
    builder.Services.AddOpenTelemetryMetrics(builder => builder
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(OtMetrics.MetricName))
        .AddAspNetCoreInstrumentation()
        .AddMeter(OtMetrics.MetricName)
        .AddOtlpExporter(options => options.Endpoint = new Uri(apiConfiguration.OpenTelemetryCollectorUrl)));

    builder.Services.AddOpenTelemetryTracing(builder => builder
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(SolutionInfo.Name))
        .AddAspNetCoreInstrumentation()
        .AddSource(SolutionInfo.Name)
        .AddOtlpExporter(options => options.Endpoint = new Uri(apiConfiguration.OpenTelemetryCollectorUrl)));

    // Add services to the container.
    //
    builder.Services.AddScoped<RequestRateLimit>();
    builder.Services.AddSingleton(new ActivitySource(SolutionInfo.Name));
    builder.Services.AddSingleton<OtMetrics>();
    builder.Services.AddSingleton<KafkaConfiguration>(kafkaConfig);
    builder.Services.AddSingleton<ProducerConfig>(kafkaConfig.BuildProducerConfig());
    builder.Services.AddSingleton<IConsumer<string, string>>(
        new ConsumerBuilder<string, string>(kafkaConfig.BuildConsumerConfig("api-collector")).Build());
    builder.Services.AddSingleton<KafkaProducerCore>();
    builder.Services.AddSingleton<IKafkaProducer<string, string>, KafkaProducer<string, string>>();
    builder.Services.AddSingleton<CollectorService>();
    builder.Services.AddSingleton<ICollector>(provider => provider.GetRequiredService<CollectorService>());
    builder.Services.AddHostedService<CollectorService>(provider => provider.GetRequiredService<CollectorService>());

    builder.Services.AddControllers();

    // Authentication
    //
    builder.Services
        .AddAuthorization()
        .AddAuthentication(apiConfiguration.AuthSettings.Scheme)
        .AddScheme<TokenAuthOptions, TokenAuthenticationHandler>(
            apiConfiguration.AuthSettings.Scheme,
            options =>
            {
                options.ApiTokenName = apiConfiguration.AuthSettings.TokenName;
                options.Scheme = apiConfiguration.AuthSettings.Scheme;
                options.ApiToken = apiConfiguration.AuthSettings.Token;
            });

    // healthcheck
    //
    builder.Services
        .AddHealthChecks()
        .AddKafka(kafkaConfig.BuildProducerConfig(), "heathcheck");

    builder.Services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.ReportApiVersions = true;
        options.ErrorResponses = new VersioningErrorProvider();
    });

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    //
    builder.Services.AddSwagger(apiConfiguration.AuthSettings.Scheme, apiConfiguration.AuthSettings.TokenName);

    

    // Setup each request processors
    //
    var app = builder.Build();

    app.UseSerilogRequestLogging();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseMiddleware<MetricRequestCounterMiddleware>();

    app.MapControllers();
    app.MapHealthChecks(
        "/health",
        new HealthCheckOptions { ResponseWriter = HealthCheckExtensions.WriteHealthResultResponse });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shutdown complete");
    Log.CloseAndFlush();
}