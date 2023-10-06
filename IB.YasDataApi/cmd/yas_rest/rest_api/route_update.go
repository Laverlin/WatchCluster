package rest_api

import (
	"net/http"

	"IB.YasDataApi/cmd/yas_rest/kafka"
	"IB.YasDataApi/abstract/command"
	"github.com/gin-gonic/gin"
	"github.com/rs/zerolog/log"
)

type UpdateRouteParams struct {
	UserToken string `uri:"token" binding:"required,min=7,max=11"`
	RouteId int32 `uri:"routeId" binding:"required"`
	RouteName string `json:"routeName"`
}

func (rest *Rest) UpdateRoute (context *gin.Context) {

		var params UpdateRouteParams
		if err := context.ShouldBindUri(&params); err != nil {
			log.Error().Err(err).Msg("Wrong URL params")
			context.JSON(http.StatusBadRequest, gin.H{"msg": "Wrong URL params", "error": err.Error()})
			return
		}

		if err := context.ShouldBindJSON(&params); err != nil {
			log.Error().Err(err).Msg("Wrong JSON params")
			context.JSON(http.StatusBadRequest, gin.H{"msg": "Wrong JSON params", "error": err.Error()})
			return
		}

		kafka.SendCommand (
			rest.Config, 
			command.CmdRenameRouteByToken, 
			command.RenameRouteByToken {
				Token: params.UserToken,
				RouteId: params.RouteId,
				RouteName: params.RouteName,
		})

		context.JSON(http.StatusOK, gin.H{"msg": "The route has been successfully updated"})
}