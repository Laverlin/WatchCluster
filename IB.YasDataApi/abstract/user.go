package abstract

import (
	"time"
)

type User struct {
	UserId       int32		`json:"userId"`
	PublicId     string		`json:"publicId"`
	TelegramId   int64		`json:"telegramId"`
	UserName     string		`json:"userName"`
	RegisterTime time.Time	`json:"registerTime"`
}