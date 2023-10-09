package dal

import (
	"context"

	"github.com/jackc/pgx/v4"
	"github.com/rs/zerolog/log"

	"IB.YasDataApi/abstract"
	"IB.YasDataApi/abstract/command"
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

func (dal *Dal) QueryUser(telegramId int64) (abstract.User, error) {

	// Get raw routes from DB
	// 
	yasUser, err := queryDb(
		dal.Config,
		func(query *yasdb.Queries, ctx context.Context) (yasdb.YasUser, error) {
			return query.GetUser(ctx, telegramId)
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

func (dal *Dal) QueryRoutes(token string, limit int32) ([]abstract.Route, error) {

	// Get raw routes from DB
	// 
	yasRoutes, err := queryDb(
		dal.Config,
		func(query *yasdb.Queries, ctx context.Context) ([]yasdb.YasRoute, error) {
			if limit > 0 {
				return query.ListRoutesWithLimit(ctx, yasdb.ListRoutesWithLimitParams{ PublicID: token, Limit: limit })
			} else {
				return query.ListRoutes(ctx, token)
			}
			
		})
	if err != nil {
		return nil, err
	}

	// Get raw waypoints form DB
	//
	yasWaypoints, err := queryDb(
		dal.Config,
		func(query *yasdb.Queries, ctx context.Context) ([]yasdb.YasWaypoint, error) {
			return query.ListWaypoints(ctx, token)
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

func (dal *Dal) ExecAddUser(u command.AddUser) {
	execDb(
		dal.Config, 
		func(query *yasdb.Queries, ctx context.Context) error {
			return query.CreateUser(ctx, yasdb.CreateUserParams { PublicID: u.Token, TelegramID: u.TelegramId, UserName: u.UserName })
		})
}

func (dal *Dal) ExecAddRoute(r command.AddRoute) (int32, error) {
	return queryDb(
		dal.Config,
		func(query *yasdb.Queries, ctx context.Context) (int32, error) {
			return query.AddRoute(ctx, yasdb.AddRouteParams { UserID: r.UserId, RouteName: r.RouteName })
		})
}

func (dal *Dal) ExecAddWaypoint(routeId int32, orderId int32, wp command.AddWaypoint) {
	execDb(
		dal.Config,
		func(query *yasdb.Queries, ctx context.Context) error {
			return query.AddWaypoint(ctx, yasdb.AddWaypointParams {
				RouteID: int64(routeId), 
				WaypointName: wp.WaypointName,
				Lat: wp.Lat,
				Lon: wp.Lon,
				OrderID: orderId,
			})
		})
}

func (dal *Dal) ExecDeleteRoute(delParams command.DeleteRoute) {
	execDb(
		dal.Config,
		func(query *yasdb.Queries, ctx context.Context) error {
			return query.DeleteRoute(ctx, yasdb.DeleteRouteParams{PublicID: delParams.Token, RouteID: delParams.RouteId })
		})
}

func (dal *Dal) ExecRenameRouteById(routeId int32, userId int64, newName string) {
	execDb(
		dal.Config,
		func(query *yasdb.Queries, ctx context.Context) error {
			return query.RenameRouteById(ctx, yasdb.RenameRouteByIdParams{UserID: userId, RouteID: routeId, RouteName: newName })
		})
}

func (dal *Dal) ExecRenameRouteByToken(renameParams command.RenameRouteByToken) {
	execDb(
		dal.Config,
		func(query *yasdb.Queries, ctx context.Context) error {
			return query.RenameRouteByToken(ctx, 
				yasdb.RenameRouteByTokenParams{
					PublicID: renameParams.Token, 
					RouteID: renameParams.RouteId, 
					RouteName: renameParams.RouteName, 
				},
			)
		})
}

type yasType interface {
	[]yasdb.YasRoute | []yasdb.YasWaypoint | yasdb.YasUser | int32
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
	if err != nil {
		var empty T
		return empty, err
	}

	return result, nil
}


type execFunc func(query *yasdb.Queries, ctx context.Context) error

// Execute query without result. Errors are not porpagated
//
func execDb(config abstract.Config, exec execFunc) {
	ctx := context.Background()
	conn, err := pgx.Connect(ctx, config.PostgreUrl)
	if err != nil {
		log.Error().Err(err).Msg("Cannot establish connection to postgres")
		return 
	}
	defer conn.Close(ctx)

	queries := yasdb.New(conn)

	err = exec(queries, ctx)
	if err != nil {
		log.Error().Err(err).Msg("Error executing query")
		return 
	}
}