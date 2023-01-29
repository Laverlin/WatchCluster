package kafka

import (
	"context"
	"encoding/json"
	"strconv"

	"IB.YasDataApi/abstract"
	"IB.YasDataApi/dal"
	"github.com/rs/zerolog/log"
	"github.com/segmentio/kafka-go"
)

func Subscribe(config abstract.Config, dal dal.Dal){
	r := kafka.NewReader(kafka.ReaderConfig{
		Brokers:   []string{ config.Kafka.Broker },
		GroupID:   "yas-consumer",
		Topic:     config.Kafka.TopicName,
		// MinBytes:  10e3, // 10KB
		// MaxBytes:  10e6, // 10MB
	})

	for {
		m, err := r.ReadMessage(context.Background())
		if err != nil {
			log.Error().Err(err).Msg("Error read message")
			continue
		}
		log.Debug().
			Str("Topic", m.Topic).
			Str("Partition", strconv.Itoa(m.Partition)).
			Str("Offset", strconv.FormatInt(m.Offset, 10)).
			Str("Key", string(m.Key)).
			Str("Value", string(m.Value)).
			Interface("Headers", m.Headers).
			Msg("got message")
		
		dispatcher(dal, m)
	}
}

func dispatcher(dal dal.Dal, message kafka.Message) {
	var command CommandType
	for _, v := range message.Headers {
		if v.Key == "command" {
			command = MapStringCommandType(string(v.Value))
		}
	}
	if command == 0 {
		log.Warn().Msg("Unknown message")
	}

	switch command {
		case AddUser:
			var addUserCommand AddUserCommand
			err := json.Unmarshal(message.Value, &addUserCommand)
			if err != nil {
				log.Error().Err(err).Msg("Unable to parse message")
				break
			}
			dal.ExecAddUser(addUserCommand.PublicId, addUserCommand.TelegramId, addUserCommand.UserName)

		default:
			log.Warn().Str("command", strconv.Itoa(int(command))).Msg("Unknown command")
	}
}

