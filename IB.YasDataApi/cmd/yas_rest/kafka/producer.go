package kafka

import (
	"context"
	"encoding/json"

	"IB.YasDataApi/abstract"
	"IB.YasDataApi/abstract/command"
	"github.com/rs/zerolog/log"
	"github.com/segmentio/kafka-go"
	"github.com/segmentio/ksuid"
)

type ICommand interface {
	command.AddRoute | command.AddUser | command.AddWaypoint | command.RenameRouteById | command.RenameRouteByToken | command.DeleteRoute
} 

func SendCommand[T ICommand](config abstract.Config, commandType string, command T) {
	w:= &kafka.Writer {
		Addr: kafka.TCP(config.Kafka.Broker),
		Topic: config.Kafka.TopicName,
		AllowAutoTopicCreation: true,
	}

	var jsonMessage, errM = json.Marshal(command)
	if errM != nil{
		log.Error().Err(errM).Msg("Unabe to marshal command to JSON")
		return
	}

	err := w.WriteMessages(
		context.Background(),
		kafka.Message {
			Key: []byte(ksuid.New().String()),
			Value: jsonMessage,
			Headers: []kafka.Header {{
				Key: "command",
				Value: []byte(commandType),
			}},
		},
	)
	if err != nil {
		log.Error().Err(err).Msg("Error push message to Kafka")
	}
	if err := w.Close(); err != nil {
		log.Error().Err(err).Msg("Error close kafka connection")
	}

}