package main

import (
	"context"
	"encoding/json"
	"strconv"

	"IB.YasDataApi/abstract"
	"IB.YasDataApi/abstract/command"
	"IB.YasDataApi/dal"
	"github.com/rs/zerolog/log"
	"github.com/segmentio/kafka-go"
)

func Subscribe(config abstract.Config, dal dal.Dal) {
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
	cmd := "unknown"
	for _, v := range message.Headers {
		if v.Key == "command" {
			cmd = string(v.Value)
		}
	}
	if cmd == "unknown" {
		log.Warn().Msg("Unknown message")
	}

	switch cmd {
		case command.CmdCreateUser:
			var addUserCommand command.AddUser
			err := json.Unmarshal(message.Value, &addUserCommand)
			if err != nil {
				log.Error().Err(err).Msg("Unable to parse add user message")
				break
			}
			dal.ExecAddUser(addUserCommand)

		case command.CmdAddRoute:
			var addRouteCommand command.AddRoute
			err := json.Unmarshal(message.Value, &addRouteCommand)
			if err != nil {
				log.Error().Err(err).Msg("Unable to parse add route message")
				break
			}
			routeId, _ := dal.ExecAddRoute(addRouteCommand)
			for id, wp := range addRouteCommand.Waypoints {
				dal.ExecAddWaypoint(routeId, int32(id), wp)
			}

		case command.CmdDeleteRoute:
			var deleteRouteCommand command.DeleteRoute
			err := json.Unmarshal(message.Value, &deleteRouteCommand)
			if err != nil {
				log.Error().Err(err).Msg("Unable to parse delete route message")
				break
			}
			dal.ExecDeleteRoute(deleteRouteCommand)

		case command.CmdRenameRouteById:
			var renameRouteCommand command.RenameRouteById
			err := json.Unmarshal(message.Value, &renameRouteCommand)
			if err != nil {
				log.Error().Err(err).Msg("Unable to parse rename route message")
				break
			}
			dal.ExecRenameRouteById(renameRouteCommand.RouteId, renameRouteCommand.UserId, renameRouteCommand.NewName)

		case command.CmdRenameRouteByToken:
			var renameRouteCommand command.RenameRouteByToken
			err := json.Unmarshal(message.Value, &renameRouteCommand)
			if err != nil {
				log.Error().Err(err).Msg("Unable to parse rename route message")
				break
			}
			dal.ExecRenameRouteByToken(renameRouteCommand)

		default:
			log.Warn().Str("command", cmd).Msg("Unknown command")
	}
}