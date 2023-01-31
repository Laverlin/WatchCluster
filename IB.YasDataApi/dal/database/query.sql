-- name: ListRoutes :many
SELECT r.* FROM yas_route r
JOIN yas_user u ON r.user_id = u.user_id 
WHERE  u.public_id = $1
ORDER BY upload_time DESC;

-- name: ListWaypoints :many
SELECT wp.* FROM yas_waypoint wp
JOIN yas_route r ON wp.route_id = r.route_id
JOIN yas_user u ON r.user_id = u.user_id
WHERE u.public_id = $1
ORDER BY wp.order_id ASC, wp.waypoint_id ASC;

-- name: GetUser :one
SELECT user_id, public_id, telegram_id, COALESCE(user_name, '') as user_name, register_time FROM yas_user WHERE telegram_id = $1;

-- name: CreateUser :exec
INSERT INTO yas_user (public_id, telegram_id, user_name, register_time)
    VALUES ($1, $2, $3, now())
ON CONFLICT DO NOTHING;

-- name: AddRoute :one
INSERT INTO yas_route (user_id, route_name, upload_time) VALUES ($1, $2, now())
RETURNING route_id;

-- name: AddWaypoint :exec
INSERT INTO yas_waypoint (route_id, waypoint_name, lat, lon, order_id) VALUES ($1, $2, $3, $4, $5);
