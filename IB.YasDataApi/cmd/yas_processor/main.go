package main

import (
	"os"

	"github.com/rs/zerolog"
	"github.com/rs/zerolog/log"

	"IB.YasDataApi/abstract"
	"IB.YasDataApi/dal"
)

func main() {

	// Setup global logger settings with defaults
	//
	zerolog.TimeFieldFormat = zerolog.TimeFormatUnix
	consoleWriter := zerolog.ConsoleWriter{Out: os.Stdout}
	multi := zerolog.MultiLevelWriter(consoleWriter, os.Stderr)
	log.Logger = zerolog.New(multi).With().
		Timestamp().
		Str("Application", "yas-processor").
		Caller().
		Logger()

	// Load config
	//
	config, err := abstract.ConfigLoad()
	if err != nil {
		log.Fatal().Err(err).Msg("Fatal: unable to load config")
	}

	// Adjust logger with config
	//
	zerolog.SetGlobalLevel(zerolog.Level(config.LogLevel))
	
	// Setup data access layer
	//
	dataLayer := dal.New(config)

	Subscribe(config, dataLayer)
}