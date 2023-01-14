package httproutes

import (
	"fmt"
	"net/http"

	"github.com/gin-gonic/gin"
	"github.com/rs/zerolog/log"
)

type GetUserParams struct {
	UserId int64 `uri:"userId" binding:"required,min=1"`
}

func (httpRoutes *HttpRoutes) GetUser (context *gin.Context) {

		var params GetUserParams
		if err := context.ShouldBindUri(&params); err != nil {
			log.Error().Err(err).Msg("Wrong user id")
			context.JSON(http.StatusBadRequest, gin.H{"msg": "Wrong user id", "error": err.Error()})
			return
		}

		fmt.Println(params.UserId)

		user, err := httpRoutes.DataLayer.QueryUser(params.UserId)
		if err != nil {
			log.Error().Err(err).Msg("Unable to get user")
			context.JSON(http.StatusBadRequest, gin.H{"msg": "Unable to get user", "error": err.Error()})
			return
		}
		if user.UserId == 0 {
			context.JSON(http.StatusNotFound, gin.H{"msg": "No User has been found"})
			return
		}
		context.JSON(http.StatusOK, user)
}