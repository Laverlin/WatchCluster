package rest_api

import (
	"net/http"

	"github.com/gin-gonic/gin"
	"github.com/jackc/pgx/v4"
	"github.com/rs/zerolog/log"
)

type GetUserParams struct {
	TelegramId int64 `uri:"telegramId" binding:"required,min=1"`
}

func (rest *Rest) GetUser (context *gin.Context) {

		var params GetUserParams
		if err := context.ShouldBindUri(&params); err != nil {
			log.Error().Err(err).Msg("Wrong user id")
			context.JSON(http.StatusBadRequest, gin.H{"msg": "Wrong user id", "error": err.Error()})
			return
		}

		user, err := rest.DataLayer.QueryUser(params.TelegramId)
		if err == pgx.ErrNoRows {
			context.JSON(http.StatusNotFound, gin.H{"msg": "No User has been found"})
			return
		}

		if err != nil {
			log.Error().Err(err).Msg("Unable to get user")
			context.JSON(http.StatusBadRequest, gin.H{"msg": "Unable to get user", "error": err.Error()})
			return
		}

		context.JSON(http.StatusOK, user)
}