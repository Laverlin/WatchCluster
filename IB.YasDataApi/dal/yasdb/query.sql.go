// Code generated by sqlc. DO NOT EDIT.
// versions:
//   sqlc v1.16.0
// source: query.sql

package yasdb

import (
	"context"
)

const createUser = `-- name: CreateUser :exec

INSERT INTO yas_user (public_id, telegram_id, user_name)
    VALUES ($1, $2, $3)
ON CONFLICT DO NOTHING
`

type CreateUserParams struct {
	PublicID   string
	TelegramID int64
	UserName   string
}

// SELECT * FROM (
//
//	SELECT user_id, public_id, telegram_id, COALESCE(user_name, '') as user_name, register_time FROM yas_user WHERE telegram_id = $1
//	UNION
//	SELECT 0 as user_id, 'empty' as public_id, 0 as telegram_id, 'dummy' as user_name, NOW() as register_time
//
// ) u
// ORDER BY user_id DESC LIMIT 1;
func (q *Queries) CreateUser(ctx context.Context, arg CreateUserParams) error {
	_, err := q.db.Exec(ctx, createUser, arg.PublicID, arg.TelegramID, arg.UserName)
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
SELECT wp.waypoint_id, wp.route_id, wp.waypoint_name, wp.lat, wp.lon, wp.order_id FROM yas_waypoint wp
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
