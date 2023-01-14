package abstract

type Waypoint struct {
	WaypointId   int32		`json:"waypointId"`
	WaypointName string		`json:"waypointName"`
	Lat          float64	`json:"lat"`
	Lon          float64	`json:"lon"`
	OrderId      int32		`json:"orderId"`
}