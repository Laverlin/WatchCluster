using Confluent.Kafka;
using IB.WatchCluster.Abstract;
using IB.WatchCluster.Abstract.Configuration;
using IB.WatchCluster.Abstract.Entity.Configuration;
using IB.WatchCluster.Api.Entity;
using IB.WatchCluster.Api.Entity.Configuration;
using IB.WatchCluster.Api.Infrastructure;
using IB.WatchCluster.Api.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using System.Diagnostics;
using IB.WatchCluster.Abstract.Kafka;
using IB.WatchCluster.Abstract.Services;
using IB.WatchCluster.Api.Middleware;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// Set Bootstrap logger
//
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();
Log.Information("Starting up");
OtelMetrics? otelMetrics = null; 

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
      .Enrich.WithProperty("version", SolutionInfo.Version)
      .Enrich.WithProperty("Application", SolutionInfo.Name)
      .Enrich.WithSpan()
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
    kafkaConfig.SetDefaults("api-collector");
    var collectorHandler = new CollectorHandler();

    // Metrics & Tracing
    //
    otelMetrics = new OtelMetrics();
    builder.Services.AddOpenTelemetryMetrics(b => b
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(otelMetrics.MetricJob))
        .AddAspNetCoreInstrumentation()
        .AddMeter(otelMetrics.MetricJob)
        .AddOtlpExporter(options => options.Endpoint = new Uri(apiConfiguration.OpenTelemetryCollectorUrl)));

    builder.Services.AddOpenTelemetryTracing(b => b
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(SolutionInfo.Name))
        .AddAspNetCoreInstrumentation()
        .AddSource(SolutionInfo.Name)
        .AddOtlpExporter(options => options.Endpoint = new Uri(apiConfiguration.OpenTelemetryCollectorUrl)));

    // Add services to the container.
    //
    builder.Services.AddScoped<RequestRateLimit>();
    builder.Services.AddSingleton(new ActivitySource(SolutionInfo.Name));
    builder.Services.AddSingleton(otelMetrics);
    builder.Services.AddSingleton(kafkaConfig);
    builder.Services.AddSingleton<IKafkaBroker, KafkaBroker>();
    builder.Services.AddSingleton(collectorHandler);
    builder.Services.AddHostedService<CollectorService>();

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
        .AddCheck("self", () => collectorHandler.IsRunning ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy())
        .AddKafka(kafkaConfig.BuildProducerConfig(), "healthcheck");

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
    app.Lifetime.ApplicationStarted.Register(() => otelMetrics.SetInstanceUp());
    app.Lifetime.ApplicationStopped.Register(() => otelMetrics.SetInstanceDown());

    app.UseSerilogRequestLogging();
    app.UseMiddleware<MetricRequestCounterMiddleware>();

    // Configure the HTTP request pipeline.
    //
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    
    app.MapControllers();
    app.MapHealthChecks(
        "/health/ready",
        new HealthCheckOptions { ResponseWriter = HealthcheckWriter.HealthResultResponseJsonFull });
    app.MapHealthChecks(
        "/health/live", 
        new HealthCheckOptions { Predicate = r => r.Name.Contains("self")});
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    otelMetrics?.SetInstanceDown();
    Log.Information("Shutdown complete");
    Log.CloseAndFlush();
}