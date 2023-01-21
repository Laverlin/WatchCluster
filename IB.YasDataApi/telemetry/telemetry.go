package telemetry

import (
	"context"

	"github.com/rs/zerolog/log"
	"go.opentelemetry.io/otel/sdk/resource"
	"go.opentelemetry.io/otel/sdk/metric"
	"go.opentelemetry.io/otel/sdk/trace"
	"go.opentelemetry.io/otel/exporters/otlp/otlpmetric/otlpmetricgrpc"
	"go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracegrpc"
	"go.opentelemetry.io/otel/metric/global"
	"go.opentelemetry.io/otel/semconv/v1.9.0"

	"IB.YasDataApi/abstract"
)

type Telemetry struct {

	// Instance of the metric provider
	//
	MeterProvider metric.MeterProvider

	// Instance of the metric provider
	//
	TraceProvider trace.TracerProvider

	// context
	//
	Ctx context.Context
}

func Setup(config abstract.Config) (Telemetry, error) {

	ctx := context.Background()
	res, err := resource.New(ctx,
		resource.WithAttributes(
			semconv.ServiceNameKey.String("wc_yas_reader"),
		),
	)
	if err != nil {
		log.Error().Err(err).Msg("Unable to create resource")
		return Telemetry{}, nil
	}

	metricExporter, err := otlpmetricgrpc.New(
		ctx, otlpmetricgrpc.WithEndpoint(config.OtelEndpoint), otlpmetricgrpc.WithInsecure())
	if err != nil {
		log.Error().Err(err).Msg("Unable to setup metrics")
		return Telemetry{}, nil
	}

	traceExporter, err := otlptracegrpc.New(
		ctx, otlptracegrpc.WithEndpoint(config.OtelEndpoint), otlptracegrpc.WithInsecure())
	if err != nil {
		log.Error().Err(err).Msg("Unable to setup traces")
		return Telemetry{}, nil
	}

	meterProvider := metric.NewMeterProvider(
		metric.WithResource(res), 
		metric.WithReader(metric.NewPeriodicReader(metricExporter)),
	)
	global.SetMeterProvider(meterProvider)

	bsp := trace.NewBatchSpanProcessor(traceExporter)
	tracerProvider := trace.NewTracerProvider(
		trace.WithSampler(trace.AlwaysSample()),
		trace.WithResource(res),
		trace.WithSpanProcessor(bsp),
	)

	return Telemetry{ MeterProvider: *meterProvider, TraceProvider: *tracerProvider, Ctx: ctx }, nil
}

func (telemetry *Telemetry) Shutdown() {
	log.Debug().Msg("Shutdown telemetry")
	if err := telemetry.MeterProvider.Shutdown(telemetry.Ctx); err != nil {
		log.Error().Err(err).Msg("Unable to shutdown metrics")
	}
	if err := telemetry.TraceProvider.Shutdown(telemetry.Ctx); err != nil {
		log.Error().Err(err).Msg("Unable to shutdown traces")
	}
}