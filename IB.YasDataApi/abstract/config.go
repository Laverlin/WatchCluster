package abstract

import (
	"os"
	"strings"

	"github.com/rs/zerolog/log"
	"github.com/knadh/koanf"
	"github.com/knadh/koanf/parsers/yaml"
	"github.com/knadh/koanf/providers/file"
	"github.com/knadh/koanf/providers/env"
	flag "github.com/spf13/pflag"
)

// define where the service is awaiting for requests
//
type Listener struct {

	// ip or host name
	//
	Host string `koanf:"host"`

	// port
	//
	Port string `koanf:"port"`
}

// returns full listener string "host:port"
//
func (listener *Listener) GetListener() string {
	return listener.Host + ":" + listener.Port
}

// configuration params
//
type Config struct {

	// Where the service is awaitng requests
	//
	Listener Listener `koanf:"listener"`

	// Postgres connection string
	//
	PostgreUrl string `koanf:"pgUrl"`

	// Level of logging -1 Trace, 0 Info, 1 Debug, 2 Warning, 3 Error, 4 Fatal, 5 Panic
	//
	LogLevel int `koanf:"logLevel"`

	// Endpoint of OpenTelemetry service for export via GRPC
	//
	OtelEndpoint string `koanf:"otelEndpoint"`
}

// Loads config data from .yaml config file and environment variables (prefix YASR_).
// The config file can be passed via command line --config option.
// The default is ./config.yaml
//
func ConfigLoad() (Config, error) {

	var config Config
	var configFile string
	 
	f := flag.NewFlagSet("config", flag.ContinueOnError)
	f.StringVar(&configFile, "config", "./config.yaml", "path to config file in yaml")
	f.Parse(os.Args[1:])

	k := koanf.New(".")
	if err := k.Load(file.Provider(configFile), yaml.Parser()); err != nil {
		log.Warn().Err(err).Msg("Unable to load config file")
	} else {
		log.Info().Str("file", configFile).Msg("config file loaded")
	}

	k.Load(env.Provider("YASR_", "_", func(s string) string {
		return strings.TrimPrefix(s, "YASR_")
	}), nil)

	if err := k.Unmarshal("", &config); err != nil {
		log.Error().Err(err).Msg("Unable to parse config")
		return Config{}, err
	}

	log.Debug().Interface("Config values", config).Send()
	return config, nil
}