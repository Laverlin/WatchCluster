package telemetry

import (
	"time"

	"github.com/gin-gonic/gin"
	"github.com/rs/zerolog/log"
	"go.opentelemetry.io/otel/attribute"
	"go.opentelemetry.io/otel/metric/instrument"
	"go.opentelemetry.io/otel/metric/unit"
	"go.opentelemetry.io/otel/propagation"
	"go.opentelemetry.io/otel/trace"
)


func Middleware(telemetry Telemetry) gin.HandlerFunc {

	requestDuration, err := telemetry.Meter.SyncInt64().Histogram(
		"wc_restapi_request_duration",
		instrument.WithUnit(unit.Milliseconds))
	if err != nil {
		log.Error().Err(err).Msg("Unable to register metric")
	}

	tracer := telemetry.TraceProvider.Tracer("IB.YasDataApi/restapi")

	return func(c *gin.Context) {
		
		// Trace request
		//
		propgator := propagation.NewCompositeTextMapPropagator(propagation.TraceContext{}, propagation.Baggage{})
		parentCtx := propgator.Extract(c.Request.Context(), propagation.HeaderCarrier(c.Request.Header))
		_, span := tracer.Start(parentCtx, "/restapi",  trace.WithAttributes(
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
			attribute.KeyValue {
				Key: "query",
				Value: attribute.StringValue(c.Request.URL.RawQuery),
			},
		)

		c.Next()
	}
}
