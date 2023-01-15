package dal

import (
	"context"
	"fmt"

	"github.com/jackc/pgx/v4"

	"IB.YasDataApi/abstract"
	"IB.YasDataApi/dal/yasdb"
)

type Dal struct {
	Config abstract.Config
}

func New(config abstract.Config) Dal {
	return Dal {
		Config: config,
	}
}

func (dal *Dal) QueryUser(userId int64) (abstract.User, error) {

	// Get raw routes from DB
	// 
	yasUser, err := queryDb(
		dal.Config,
		func(query *yasdb.Queries, ctx context.Context) (yasdb.YasUser, error) {
			return query.GetUser(ctx, userId)
		})

	if err != nil {
		return abstract.User{}, err
	}

	return abstract.User {
		UserId: yasUser.UserID,
		PublicId: yasUser.PublicID,
		TelegramId: yasUser.TelegramID,
		UserName: yasUser.UserName,
		RegisterTime: yasUser.RegisterTime,
	}, nil
}

func (dal *Dal) QueryRoutes(userId string) ([]abstract.Route, error) {

	// Get raw routes from DB
	// 
	yasRoutes, err := queryDb(
		dal.Config,
		func(query *yasdb.Queries, ctx context.Context) ([]yasdb.YasRoute, error) {
			return query.ListRoutes(ctx, userId)
		})
	if err != nil {
		return nil, err
	}

	// Get raw waypoints form DB
	//
	yasWaypoints, err := queryDb(
		dal.Config,
		func(query *yasdb.Queries, ctx context.Context) ([]yasdb.YasWaypoint, error) {
			return query.ListWaypoints(ctx, userId)
		})
	if err != nil {
		return nil, err
	}

	// construct list of routes
	//
	var routes []abstract.Route
	for _, r := range yasRoutes {
		var waypoints []abstract.Waypoint
		for _, w := range yasWaypoints {
			if w.RouteID == int64(r.RouteID) {
				waypoints = append(waypoints, abstract.Waypoint{
					WaypointId: w.WaypointID,
					WaypointName: w.WaypointName,
					Lat: w.Lat,
					Lon: w.Lon,
					OrderId: w.OrderID,
				})
			}
		}
		routes = append(routes, abstract.Route {
			RouteId:    r.RouteID,
			UserId:     r.UserID,
			RouteName:  r.RouteName,
			UploadTime: r.UploadTime,
			Waypoints:  waypoints,
		})
	}

	return routes, nil
}

type yasType interface {
	[]yasdb.YasRoute | []yasdb.YasWaypoint | yasdb.YasUser
}

type queryFunc[T yasType] func(query *yasdb.Queries, ctx context.Context) (T, error)

func queryDb[T yasType](config abstract.Config, query queryFunc[T]) (T, error) {
	ctx := context.Background()
	conn, err := pgx.Connect(ctx, config.PostgreUrl)
	if err != nil {
		var empty T
		return empty, err
	}
	defer conn.Close(ctx)

	queries := yasdb.New(conn)

	result, err := query(queries, ctx)

	fmt.Println(result)

	if err != nil {
		var empty T
		return empty, err
	}

	return result, nil
}
