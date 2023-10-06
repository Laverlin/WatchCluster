// Code generated by sqlc. DO NOT EDIT.
// versions:
//   sqlc v1.22.0
// source: query.sql

package yasdb

import (
	"context"
)

const addRoute = `-- name: AddRoute :one
INSERT INTO yas_route (user_id, route_name, upload_time) VALUES ($1, $2, now())
RETURNING route_id
`

type AddRouteParams struct {
	UserID    int64
	RouteName string
}

func (q *Queries) AddRoute(ctx context.Context, arg AddRouteParams) (int32, error) {
	row := q.db.QueryRow(ctx, addRoute, arg.UserID, arg.RouteName)
	var route_id int32
	err := row.Scan(&route_id)
	return route_id, err
}

const addWaypoint = `-- name: AddWaypoint :exec
INSERT INTO yas_waypoint (route_id, waypoint_name, lat, lon, order_id) VALUES ($1, $2, $3, $4, $5)
`

type AddWaypointParams struct {
	RouteID      int64
	WaypointName string
	Lat          float64
	Lon          float64
	OrderID      int32
}

func (q *Queries) AddWaypoint(ctx context.Context, arg AddWaypointParams) error {
	_, err := q.db.Exec(ctx, addWaypoint,
		arg.RouteID,
		arg.WaypointName,
		arg.Lat,
		arg.Lon,
		arg.OrderID,
	)
	return err
}

const createUser = `-- name: CreateUser :exec
INSERT INTO yas_user (public_id, telegram_id, user_name, register_time)
    VALUES ($1, $2, $3, now())
ON CONFLICT DO NOTHING
`

type CreateUserParams struct {
	PublicID   string
	TelegramID int64
	UserName   string
}

func (q *Queries) CreateUser(ctx context.Context, arg CreateUserParams) error {
	_, err := q.db.Exec(ctx, createUser, arg.PublicID, arg.TelegramID, arg.UserName)
	return err
}

const deleteRoute = `-- name: DeleteRoute :exec
DELETE FROM yas_route WHERE route_id = $1 AND user_id = (SELECT user_id FROM yas_user WHERE public_id = $2)
`

type DeleteRouteParams struct {
	RouteID  int32
	PublicID string
}

func (q *Queries) DeleteRoute(ctx context.Context, arg DeleteRouteParams) error {
	_, err := q.db.Exec(ctx, deleteRoute, arg.RouteID, arg.PublicID)
	return err
}

const getUser = `-- name: GetUser :one
SELECT user_id, public_id, telegram_id, COALESCE(user_name, '') as user_name, register_time FROM yas_user WHERE telegram_id = $1
`

func (q *Queries) GetUser(ctx context.Context, telegramID int64) (YasUser, error) {
	row := q.db.QueryRow(ctx, getUser, telegramID)
	var i YasUser
	err := row.Scan(
		&i.UserID,
		&i.PublicID,
		&i.TelegramID,
		&i.UserName,
		&i.RegisterTime,
	)
	return i, err
}

const listRoutes = `-- name: ListRoutes :many
SELECT r.route_id, r.user_id, r.route_name, r.upload_time FROM yas_route r
JOIN yas_user u ON r.user_id = u.user_id 
WHERE  u.public_id = $1
ORDER BY upload_time DESC
`

func (q *Queries) ListRoutes(ctx context.Context, publicID string) ([]YasRoute, error) {
	rows, err := q.db.Query(ctx, listRoutes, publicID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var items []YasRoute
	for rows.Next() {
		var i YasRoute
		if err := rows.Scan(
			&i.RouteID,
			&i.UserID,
			&i.RouteName,
			&i.UploadTime,
		); err != nil {
			return nil, err
		}
		items = append(items, i)
	}
	if err := rows.Err(); err != nil {
		return nil, err
	}
	return items, nil
}

const listWaypoints = `-- name: ListWaypoints :many
SELECT wp.waypoint_id, wp.route_id, COALESCE(wp.waypoint_name, '') as waypoint_name, wp.lat, wp.lon, wp.order_id FROM yas_waypoint wp
JOIN yas_route r ON wp.route_id = r.route_id
JOIN yas_user u ON r.user_id = u.user_id
WHERE u.public_id = $1
ORDER BY wp.order_id ASC, wp.waypoint_id ASC
`

func (q *Queries) ListWaypoints(ctx context.Context, publicID string) ([]YasWaypoint, error) {
	rows, err := q.db.Query(ctx, listWaypoints, publicID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var items []YasWaypoint
	for rows.Next() {
		var i YasWaypoint
		if err := rows.Scan(
			&i.WaypointID,
			&i.RouteID,
			&i.WaypointName,
			&i.Lat,
			&i.Lon,
			&i.OrderID,
		); err != nil {
			return nil, err
		}
		items = append(items, i)
	}
	if err := rows.Err(); err != nil {
		return nil, err
	}
	return items, nil
}

const renameRouteById = `-- name: RenameRouteById :exec
UPDATE yas_route SET route_name = $3 WHERE route_id = $1 AND user_id = $2
`

type RenameRouteByIdParams struct {
	RouteID   int32
	UserID    int64
	RouteName string
}

func (q *Queries) RenameRouteById(ctx context.Context, arg RenameRouteByIdParams) error {
	_, err := q.db.Exec(ctx, renameRouteById, arg.RouteID, arg.UserID, arg.RouteName)
	return err
}

const renameRouteByToken = `-- name: RenameRouteByToken :exec
UPDATE yas_route SET route_name = $3 WHERE route_id = $1 AND user_id = (SELECT user_id FROM yas_user WHERE public_id = $2)
`

type RenameRouteByTokenParams struct {
	RouteID   int32
	PublicID  string
	RouteName string
}

func (q *Queries) RenameRouteByToken(ctx context.Context, arg RenameRouteByTokenParams) error {
	_, err := q.db.Exec(ctx, renameRouteByToken, arg.RouteID, arg.PublicID, arg.RouteName)
	return err
}
