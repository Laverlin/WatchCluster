package main

import (
	"os"

	"github.com/gin-contrib/logger"
	"github.com/gin-gonic/gin"
	"github.com/rs/zerolog"
	"github.com/rs/zerolog/log"

	"IB.YasDataApi/abstract"
	"IB.YasDataApi/cmd/yas_rest/rest_api"
	"IB.YasDataApi/dal"
	"IB.YasDataApi/telemetry"
)

func main() {

	// Setup global logger settings with defaults
	//
	zerolog.TimeFieldFormat = zerolog.TimeFormatUnix
	consoleWriter := zerolog.ConsoleWriter{Out: os.Stdout}
	multi := zerolog.MultiLevelWriter(consoleWriter, os.Stderr)
	log.Logger = zerolog.New(multi).With().
		Timestamp().
		Str("Application", "YasDataReaderApi").
		Caller().
		Logger()

	// Load config
	//
	config, err := abstract.ConfigLoad() //.LoadConfig()
	if err != nil {
		log.Fatal().Err(err).Msg("Fatal: unable to load config")
	}

	// Adjust logger with config
	//
	zerolog.SetGlobalLevel(zerolog.Level(config.LogLevel))
	
	// Setup data access layer
	//
	dataLayer := dal.New(config)

	// Setup http routes
	//
	rest_api := rest_api.New(config, dataLayer)
	
	// Setup & run http server
	//
	router := gin.New()
	router.Use(
		logger.SetLogger(
			logger.WithLogger(func(c *gin.Context, l zerolog.Logger) zerolog.Logger {
				return log.Logger
	})))
	router.Use(gin.Recovery())

	tel, _ := telemetry.Setup(config)
	defer tel.Shutdown()

	router.Use(telemetry.Middleware(tel))

	router.GET("/user-store/users/:telegramId", rest_api.GetUser)
	router.GET("/route-store/users/:token/routes", rest_api.GetRouteList)
	router.PUT("/route-store/users/:token/routes/:routeId", rest_api.UpdateRoute)
	router.DELETE("/route-store/users/:token/routes/:routeId", rest_api.DeleteRoute)
	
	
	router.Run(config.Listener.GetListener())
}