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

	viper.SetDefault("LogLevel", 2) // Warning

	err := viper.ReadInConfig()
	if err == nil {
		err = viper.Unmarshal(&config)
		if err != nil {
			log.Error().Err(err).Msg("Unable to parse config")
			return Config{}, err
		}
		log.Info().Str("file", viper.ConfigFileUsed()).Msg("Config loaded")
	} else {
		log.Warn().Err(err).Msg("Unable to load config file")
	}

	viper.BindEnv("PostgreUrl", "pgUrl")
	viper.BindEnv("LogLevel")

	log.Debug().Interface("Config values", config).Send()
	return config, nil
}

