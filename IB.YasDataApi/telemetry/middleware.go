package telemetry

import (
	"time"

	"github.com/gin-gonic/gin"
	"github.com/rs/zerolog/log"
	"go.opentelemetry.io/otel/attribute"
	"go.opentelemetry.io/otel/metric/instrument"
	"go.opentelemetry.io/otel/metric/unit"
	"go.opentelemetry.io/otel/trace"
)


func Middleware(telemetry Telemetry) gin.HandlerFunc {

	requestDuration, err := telemetry.MeterProvider.Meter("IB.YasDataApi/reader").SyncInt64().Histogram(
		"wc_reader_request_duration",
		instrument.WithUnit(unit.Milliseconds))
	if err != nil {
		log.Error().Err(err).Msg("Unable to register metric")
	}

	tracer := telemetry.TraceProvider.Tracer("IB.YasDataApi/reader")

	return func(c *gin.Context) {
		
		// Trace request
		//
		_, span := tracer.Start(c.Request.Context(), "wc_reader_tracer",  trace.WithAttributes(
			attribute.KeyValue {
				Key: "path",
				Value: attribute.StringValue(c.Request.URL.Path),
			},
		))	
		defer span.End()

		// Meter request
		//
		startTime := time.Now()
		defer requestDuration.Record(c.Request.Context(), time.Since(startTime).Milliseconds(), 
			attribute.KeyValue {
				Key: "status",
				Value: attribute.IntValue(c.Writer.Status()),
			},
			attribute.KeyValue {
				Key: "path",
				Value: attribute.StringValue(c.Request.URL.Path),
			},
		)

		c.Next()
	}
}
