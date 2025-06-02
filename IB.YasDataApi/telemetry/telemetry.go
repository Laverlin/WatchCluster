package telemetry

import (
	"context"
	"strings"
	"time"

	"github.com/rs/zerolog/log"
	"go.opentelemetry.io/otel/exporters/otlp/otlpmetric/otlpmetricgrpc"
	"go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracegrpc"
	"go.opentelemetry.io/otel/metric"
	"go.opentelemetry.io/otel/metric/global"
	"go.opentelemetry.io/otel/metric/instrument"
	"go.opentelemetry.io/otel/metric/instrument/asyncint64"
	"go.opentelemetry.io/otel/metric/unit"
	sdkmetric "go.opentelemetry.io/otel/sdk/metric"
	"go.opentelemetry.io/otel/sdk/resource"
	"go.opentelemetry.io/otel/sdk/trace"
	semconv "go.opentelemetry.io/otel/semconv/v1.9.0"

	"IB.YasDataApi/abstract"
)

type Telemetry struct {

	// Instance of the metric provider
	//
	MeterProvider sdkmetric.MeterProvider

	// actual meter
	//
	Meter metric.Meter

	// Instance of the metric provider
	//
	TraceProvider trace.TracerProvider

	// context
	//
	Ctx context.Context

	uptimeGauge asyncint64.Gauge
}

func Setup(config abstract.Config) (Telemetry, error) {

	ctx := context.Background()
	res, err := resource.New(ctx, resource.WithAttributes(semconv.ServiceNameKey.String("IB.YasDataReader/restapi")))
	if err != nil {
		log.Error().Err(err).Msg("Unable to create resource")
		return Telemetry{}, nil
	}

	// Strip http:// prefix from endpoint if present
	endpoint := strings.TrimPrefix(config.OtelEndpoint, "http://")

	metricExporter, err := otlpmetricgrpc.New(
		ctx, otlpmetricgrpc.WithEndpoint(endpoint), otlpmetricgrpc.WithInsecure())
	if err != nil {
		log.Error().Err(err).Msg("Unable to setup metrics")
		return Telemetry{}, nil
	}

	traceExporter, err := otlptracegrpc.New(
		ctx, otlptracegrpc.WithEndpoint(endpoint), otlptracegrpc.WithInsecure())
	if err != nil {
		log.Error().Err(err).Msg("Unable to setup traces")
		return Telemetry{}, nil
	}

	meterProvider := sdkmetric.NewMeterProvider(
		sdkmetric.WithResource(res), 
		sdkmetric.WithReader(sdkmetric.NewPeriodicReader(metricExporter)),
	)
	global.SetMeterProvider(meterProvider)
	meter := meterProvider.Meter("IB.YasDataApi/restapi")
	gauge, err := meter.AsyncInt64().
		Gauge("wc_reader_uptime_gauge", instrument.WithUnit(unit.Milliseconds))
	if err != nil {
		log.Error().Err(err).Msg("Unable to setup uptime gauge")
		return Telemetry{}, nil
	}

	bsp := trace.NewBatchSpanProcessor(traceExporter)
	tracerProvider := trace.NewTracerProvider(
		trace.WithSampler(trace.AlwaysSample()),
		trace.WithResource(res),
		trace.WithSpanProcessor(bsp),
	)

	_telemetry := Telemetry{ MeterProvider: *meterProvider, Meter: meter, TraceProvider: *tracerProvider, Ctx: ctx, uptimeGauge: gauge }
	_telemetry.SetUptimeGauge(time.Now().UnixMilli())
	return _telemetry, nil
}

func (telemetry *Telemetry) SetUptimeGauge(milTime int64) {
	telemetry.Meter.RegisterCallback([]instrument.Asynchronous{ telemetry.uptimeGauge }, func(ctx context.Context) {
      telemetry.uptimeGauge.Observe(ctx, milTime)
	})
}

func (telemetry *Telemetry) Shutdown() {
	log.Debug().Msg("Shutdown telemetry")
	telemetry.SetUptimeGauge(0)
	if err := telemetry.MeterProvider.Shutdown(telemetry.Ctx); err != nil {
		log.Error().Err(err).Msg("Unable to shutdown metrics")
	}
	if err := telemetry.TraceProvider.Shutdown(telemetry.Ctx); err != nil {
		log.Error().Err(err).Msg("Unable to shutdown traces")
	}
}