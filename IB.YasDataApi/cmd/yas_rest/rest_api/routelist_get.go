package rest_api

import (
	"net/http"

	"github.com/gin-gonic/gin"
	"github.com/rs/zerolog/log"
)

type RouteListParams struct {
	UserToken string 	`uri:"token" binding:"required,min=6,max=10"`
	Limit int32			`form:"limit"`
}

func (rest *Rest) GetRouteList (context *gin.Context) {

		var params RouteListParams
		if err := context.ShouldBindUri(&params); err != nil {
			log.Error().Err(err).Msg("Wrong user id")
			context.JSON(http.StatusBadRequest, gin.H{"msg": "Wrong user id", "error": err.Error()})
			return
		}

		if err := context.ShouldBindQuery(&params); err != nil {
			log.Error().Err(err).Msg("Error bing limit")
			context.JSON(http.StatusBadRequest, gin.H{"msg": "error limit binding", "error": err.Error()})
			return
		}

		routes, err := rest.DataLayer.QueryRoutes(params.UserToken, params.Limit)

		
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