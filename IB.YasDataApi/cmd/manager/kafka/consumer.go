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
	command := "unknown"
	for _, v := range message.Headers {
		if v.Key == "command" {
			command = string(v.Value)
		}
	}
	if command == "unknown" {
		log.Warn().Msg("Unknown message")
	}

	switch command {
		case CmdCreateUser:
			var addUserCommand AddUserCommand
			err := json.Unmarshal(message.Value, &addUserCommand)
			if err != nil {
				log.Error().Err(err).Msg("Unable to parse add user message")
				break
			}
			dal.ExecAddUser(addUserCommand.PublicId, addUserCommand.TelegramId, addUserCommand.UserName)

		case CmdAddRoute:
			var addRouteCommand AddRouteCommand
			err := json.Unmarshal(message.Value, &addRouteCommand)
			if err != nil {
				log.Error().Err(err).Msg("Unable to parse add route message")
				break
			}
			routeId, _ := dal.ExecAddRoute(addRouteCommand.UserId, addRouteCommand.RouteName)
			for id, wp := range addRouteCommand.Waypoints {
				dal.ExecAddWaypoint(routeId, wp.WaypointName, wp.Lat, wp.Lon, int32(id))
			}

		case CmdDeleteRoute:
			var deleteRouteCommand DeleteRouteCommand
			err := json.Unmarshal(message.Value, &deleteRouteCommand)
			if err != nil {
				log.Error().Err(err).Msg("Unable to parse delete route message")
				break
			}
			dal.ExecDeleteRoute(deleteRouteCommand.RouteId, deleteRouteCommand.UserId)

		case CmdRenameRoute:
			var renameRouteCommand RenameRouteCommand
			err := json.Unmarshal(message.Value, &renameRouteCommand)
			if err != nil {
				log.Error().Err(err).Msg("Unable to parse rename route message")
				break
			}
			dal.ExecRenameRoute(renameRouteCommand.RouteId, renameRouteCommand.UserId, renameRouteCommand.NewName)

		default:
			log.Warn().Str("command", command).Msg("Unknown command")
	}
}