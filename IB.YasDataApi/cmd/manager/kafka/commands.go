package kafka




const (
    CmdCreateUser = "create-user"
    CmdAddRoute = "add-route"
    CmdDeleteRoute = "delete-route"
    CmdRenameRoute = "rename-route"
)

type AddUserCommand struct {
	TelegramId int64
	PublicId string
	UserName string
}

type AddWaypointCommand struct {
    WaypointName string		`json:"waypointName"`
	Lat          float64	`json:"lat"`
	Lon          float64	`json:"lon"`
}

type AddRouteCommand struct {
    UserId int64
    RouteName string
    Waypoints []AddWaypointCommand
}

type RenameRouteCommand struct {
    RouteId int32
    UserId int64
    NewName string
}

type DeleteRouteCommand struct {
    RouteId int32
    UserId int64
}

