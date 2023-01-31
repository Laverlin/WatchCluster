CREATE TABLE yas_user(
    user_id SERIAL NOT NULL,
    public_id character varying NOT NULL,
    telegram_id bigint NOT NULL,
    user_name character varying NOT NULL DEFAULT '',
    register_time timestamp with time zone NOT NULL
);
CREATE UNIQUE INDEX ixu_publicid ON "yas_user" USING btree ("public_id");
CREATE UNIQUE INDEX ixu_telegramid ON "yas_user" USING btree ("telegram_id");
CREATE UNIQUE INDEX ixu_userid ON "yas_user" USING btree ("user_id");


CREATE TABLE yas_route(
    route_id SERIAL NOT NULL PRIMARY KEY,
    user_id bigint NOT NULL,
    route_name character varying NOT NULL DEFAULT '',
    upload_time timestamp with time zone NOT NULL default (now() at time zone 'utc')
);
CREATE UNIQUE INDEX ixu_route_routeid ON "yas_route" USING btree ("route_id");
CREATE INDEX ixu_route_userid ON "yas_route" USING btree ("user_id");




CREATE TABLE yas_waypoint(
    waypoint_id SERIAL NOT NULL,
    route_id bigint NOT NULL,
    waypoint_name character varying NOT NULL DEFAULT '',
    lat numeric,
    lon numeric,
    order_id integer NOT NULL DEFAULT 0
);
CREATE INDEX ix_waypoint_routeid ON "yas_waypoint" USING btree ("route_id");
CREATE INDEX ixu_waypointid ON "yas_waypoint" USING btree ("waypoint_id");