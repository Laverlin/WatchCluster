package command

const (
    CmdCreateUser = "create-user"
    CmdAddRoute = "add-route"
    CmdDeleteRoute = "delete-route"
    CmdRenameRouteById = "rename-route-id"
    CmdRenameRouteByToken = "rename-route-token"
)

type AddUser struct {
	TelegramId int64    `json:"telegramId"`
	PublicId string     `json:"publicIs"`
	UserName string     `json:"userName"`
}

type AddWaypoint struct {
    WaypointName string		`json:"waypointName"`
	Lat          float64	`json:"lat"`
	Lon          float64	`json:"lon"`
}

type AddRoute struct {
    UserId int64             `json:"userId"`
    RouteName string         `json:"routeName"`
    Waypoints []AddWaypoint  `json:"waypoints"`
}

type RenameRouteById struct {
    UserId int64    `json:"userId"`
    RouteId int32   `json:"routerId"`
    NewName string  `json:"newName"`
}

type RenameRouteByToken struct {
    Token string        `json:"token"`
    RouteId int32       `json:"routeId"`
    RouteName string    `json:"routeName"`
}

type DeleteRoute struct {
    Token string    `json:"token"`
    RouteId int32   `json:"routeId"`
}

