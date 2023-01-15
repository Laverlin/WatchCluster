package abstract

import (
	"time"
)

type Route struct {
	RouteId    int32		`json:"routeId"`
	UserId     int64		`json:"userId"`
	RouteName  string		`json:"routeName"`
	UploadTime time.Time	`json:"uploadTime"`
	Waypoints  []Waypoint	`json:"waypoints"`
}
