package main

import (
	"os"

	"github.com/gin-gonic/gin"
	"github.com/gin-contrib/logger"
	"github.com/rs/zerolog"
	"github.com/rs/zerolog/log"

	"IB.YasDataApi/abstract"
	"IB.YasDataApi/dal"
	"IB.YasDataApi/cmd/reader/httproutes"
)

func main() {

	// Setup global logger settings with defaults
	//
	zerolog.TimeFieldFormat = zerolog.TimeFormatUnix
	consoleWriter := zerolog.ConsoleWriter{Out: os.Stdout}
	multi := zerolog.MultiLevelWriter(consoleWriter, os.Stderr)
	log.Logger = zerolog.New(multi).With().
		Timestamp().
		Str("app", "YasDataReaderApi").
		Caller().
		Logger()

	// Load config
	//
	config, err := abstract.LoadConfig()
	if err != nil {
		log.Fatal().Stack().Err(err).Msg("Fatal: unable to load config")
	}

	// Adjust logger with config
	//
	zerolog.SetGlobalLevel(zerolog.Level(config.LogLevel))
	
	// Setup data access layer
	//
	dataLayer := dal.New(config)

	// Setup http routes
	//
	httpRoutes := httproutes.New(config, dataLayer)
	
	// Setup & run http server
	//
	router := gin.New()
	router.Use(logger.SetLogger())
	router.Use(gin.Recovery())

	router.GET("/user-store/users/:userId", httpRoutes.GetUser)
	router.GET("/route-store/users/:userId/routes", httpRoutes.RouteList)
	
	router.Run(":8989")
}