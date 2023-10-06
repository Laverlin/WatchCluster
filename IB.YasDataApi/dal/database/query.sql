-- name: ListRoutes :many
SELECT r.* FROM yas_route r
JOIN yas_user u ON r.user_id = u.user_id 
WHERE  u.public_id = $1
ORDER BY upload_time DESC;

-- name: ListRoutesWithLimit :many
SELECT r.* FROM yas_route r
JOIN yas_user u ON r.user_id = u.user_id 
WHERE  u.public_id = $1
ORDER BY upload_time DESC
LIMIT $2;

-- name: ListWaypoints :many
SELECT wp.waypoint_id, wp.route_id, COALESCE(wp.waypoint_name, '') as waypoint_name, wp.lat, wp.lon, wp.order_id FROM yas_waypoint wp
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

-- name: DeleteRoute :exec
DELETE FROM yas_route WHERE route_id = $1 AND user_id = (SELECT user_id FROM yas_user WHERE public_id = $2);

-- name: RenameRouteById :exec
UPDATE yas_route SET route_name = $3 WHERE route_id = $1 AND user_id = $2;

-- name: RenameRouteByToken :exec
UPDATE yas_route SET route_name = $3 WHERE route_id = $1 AND user_id = (SELECT user_id FROM yas_user WHERE public_id = $2);
