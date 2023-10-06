package rest_api

import (
	"net/http"

	"IB.YasDataApi/cmd/yas_rest/kafka"
	"IB.YasDataApi/abstract/command"
	"github.com/gin-gonic/gin"
	"github.com/rs/zerolog/log"
)

type DeleteRouteParams struct {
	UserToken string `uri:"token" binding:"required,min=7,max=11"`
	RouteId int32 `uri:"routeId" binding:"required"`
}

func (rest *Rest) DeleteRoute (context *gin.Context) {

		var params DeleteRouteParams
		if err := context.ShouldBindUri(&params); err != nil {
			log.Error().Err(err).Msg("Wrong URL params")
			context.JSON(http.StatusBadRequest, gin.H{"msg": "Wrong URL params", "error": err.Error()})
			return
		}

		kafka.SendCommand (
			rest.Config, 
			command.CmdDeleteRoute, 
			command.DeleteRoute {
				Token: params.UserToken,
				RouteId: params.RouteId,
		})

		context.JSON(http.StatusOK, gin.H{"msg": "The route has been successfully deleted"})
}