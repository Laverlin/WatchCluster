package httproutes

import (
	"net/http"

	"github.com/gin-gonic/gin"
	"github.com/rs/zerolog/log"
)

type RouteListParams struct {
	UserId string `uri:"userId" binding:"required,min=6,max=10"`
}

func (httpRoutes *HttpRoutes) GetRouteList (context *gin.Context) {

		var params RouteListParams
		if err := context.ShouldBindUri(&params); err != nil {
			log.Error().Err(err).Msg("Wrong user id")
			context.JSON(http.StatusBadRequest, gin.H{"msg": "Wrong user id", "error": err.Error()})
			return
		}

		routes, err := httpRoutes.DataLayer.QueryRoutes(params.UserId)
		if err != nil {
			log.Error().Err(err).Msg("Unable to get route")
			context.JSON(http.StatusBadRequest, gin.H{"msg": "Unable to get route", "error": err.Error()})
			return
		}
		if routes == nil {
			context.JSON(http.StatusNotFound, gin.H{"msg": "No User/Routes has been found"})
			return
		}
		context.JSON(http.StatusOK, routes)
}