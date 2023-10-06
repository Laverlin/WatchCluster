// Code generated by sqlc. DO NOT EDIT.
// versions:
//   sqlc v1.22.0

package yasdb

import (
	"time"
)

type YasRoute struct {
	RouteID    int32
	UserID     int64
	RouteName  string
	UploadTime time.Time
}

type YasUser struct {
	UserID       int32
	PublicID     string
	TelegramID   int64
	UserName     string
	RegisterTime time.Time
}

type YasWaypoint struct {
	WaypointID   int32
	RouteID      int64
	WaypointName string
	Lat          float64
	Lon          float64
	OrderID      int32
}
