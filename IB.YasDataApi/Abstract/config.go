package abstract

import (
	"github.com/rs/zerolog/log"
	"github.com/spf13/viper"
)

type Config struct {
	PostgreUrl string `mapstructure:"pgUrl"`
	LogLevel int `mapstructure:"logLevel"`
}

func LoadConfig() (Config, error) {
	var config Config
	viper.AddConfigPath(".")
    viper.SetConfigName("config")
	viper.SetConfigType("yaml")

	viper.SetDefault("LogFile", "ReaderDataApi.log")
	viper.SetDefault("LogLevel", 2) // Warning

	viper.AutomaticEnv()
	viper.BindEnv("PGURL")
	viper.BindEnv("LOGLEVEL")

	err := viper.ReadInConfig()
	if err != nil {
		log.Error().Err(err).Msg("Config not found")
		return Config{}, err
	}

	err = viper.Unmarshal(&config)
	if err != nil {
		log.Error().Err(err).Msg("Unable to parse config")
		return Config{}, err
	}

	log.Info().Str("file", viper.ConfigFileUsed()).Msg("Config loaded")
	log.Debug().Interface("Config values", config).Send()
	return config, nil
}

