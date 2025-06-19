--
-- PostgreSQL database dump
--

-- Dumped from database version 15.4
-- Dumped by pg_dump version 15.4

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: deviceinfo_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.deviceinfo_id_seq
    START WITH 7
    INCREMENT BY 1
    NO MINVALUE
    MAXVALUE 2147483647
    CACHE 1;


ALTER TABLE public.deviceinfo_id_seq OWNER TO postgres;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: DeviceInfo; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."DeviceInfo" (
    id integer DEFAULT nextval('public.deviceinfo_id_seq'::regclass) NOT NULL,
    "DeviceId" character varying NOT NULL,
    "DeviceName" character varying,
    "FirstRequestTime" timestamp without time zone DEFAULT now()
);


ALTER TABLE public."DeviceInfo" OWNER TO postgres;

--
-- Name: add_device(character varying); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.add_device(device_id character varying) RETURNS SETOF public."DeviceInfo"
    LANGUAGE plpgsql
    AS $$
BEGIN
RETURN QUERY
WITH input_rows("DeviceId") AS (SELECT device_id)  -- see above
, ins AS (
   INSERT INTO "DeviceInfo" AS D ("DeviceId") 
   SELECT I."DeviceId" FROM input_rows I
   ON CONFLICT ("DeviceId") DO NOTHING
   RETURNING id, "DeviceId"                   -- we need unique columns for later join
   )
, sel AS (
   SELECT id, "DeviceId"
   FROM   ins
   UNION  ALL
   SELECT id, "DeviceId"
   FROM   input_rows
   JOIN   "DeviceInfo" D USING ("DeviceId")
   )
, ups AS (                                      -- RARE corner case
   INSERT INTO "DeviceInfo" AS D ("DeviceId")  -- another UPSERT, not just UPDATE
   SELECT I."DeviceId"
   FROM   input_rows I
   LEFT   JOIN sel S USING ("DeviceId")     -- columns of unique index
   WHERE  S."DeviceId" IS NULL                         -- missing!
   ON     CONFLICT ("DeviceId") DO UPDATE     -- we've asked nicely the 1st time ...
   SET   "DeviceId" = D."DeviceId"                          -- ... this time we overwrite with old value
   -- SET name = EXCLUDED.name                  -- alternatively overwrite with *new* value
   RETURNING id, "DeviceId"  --, usr, contact               -- return more columns?
   )
SELECT sel.id, sel."DeviceId" FROM sel
UNION  ALL
TABLE  ups;
END
$$;


ALTER FUNCTION public.add_device(device_id character varying) OWNER TO postgres;

--
-- Name: add_device(character varying, character varying); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.add_device(device_id character varying, device_name character varying) RETURNS TABLE(id integer, "DeviceId" character varying, "DeviceName" character varying)
    LANGUAGE sql
    AS $$

WITH input_rows("DeviceId", "DeviceName") AS (SELECT device_id, device_name)  -- see above
, ins AS (
   INSERT INTO "DeviceInfo" AS D ("DeviceId", "DeviceName") 
   SELECT I."DeviceId", I."DeviceName" FROM input_rows I
   ON CONFLICT ("DeviceId") DO NOTHING
--SET "DeviceName" = EXCLUDED."DeviceName"
   RETURNING id, "DeviceId", "DeviceName"                   -- we need unique columns for later join
   )
, sel AS (
   SELECT id, "DeviceId", ins."DeviceName"
   FROM   ins
   UNION  ALL
   SELECT id, "DeviceId", I."DeviceName"
   FROM   input_rows I
   JOIN   "DeviceInfo" D USING ("DeviceId")
   )
, ups AS (                                      -- RARE corner case
   INSERT INTO "DeviceInfo" AS D ("DeviceId", "DeviceName")  -- another UPSERT, not just UPDATE
   SELECT I."DeviceId", I."DeviceName"
   FROM   input_rows I
   LEFT   JOIN sel S USING ("DeviceId")     -- columns of unique index
   WHERE  S."DeviceId" IS NULL                         -- missing!
   ON     CONFLICT ("DeviceId") DO UPDATE     -- we've asked nicely the 1st time ...
   SET   "DeviceId" = D."DeviceId"                          -- ... this time we overwrite with old value
      --"DeviceName" = EXCLUDED."DeviceName"                  -- alternatively overwrite with *new* value
   RETURNING id, "DeviceId", "DeviceName"  --, usr, contact               -- return more columns?
   )
SELECT sel.id, sel."DeviceId", "DeviceName" FROM sel
UNION  ALL
TABLE  ups;

$$;


ALTER FUNCTION public.add_device(device_id character varying, device_name character varying) OWNER TO postgres;

--
-- Name: refresh_view(regclass); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.refresh_view(_view_name regclass) RETURNS void
    LANGUAGE plpgsql
    AS $$
BEGIN
    EXECUTE 'REFRESH MATERIALIZED VIEW ' || _view_name;
    INSERT INTO view_state (view_name, refresh_time) VALUES (_view_name::text, NOW())
    ON CONFLICT (view_name) DO 
        UPDATE SET refresh_time = excluded.refresh_time WHERE view_state.view_name = _view_name::text;
end;
$$;


ALTER FUNCTION public.refresh_view(_view_name regclass) OWNER TO postgres;

--
-- Name: cityrequest_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.cityrequest_id_seq
    START WITH 14
    INCREMENT BY 1
    NO MINVALUE
    MAXVALUE 2147483647
    CACHE 1;


ALTER TABLE public.cityrequest_id_seq OWNER TO postgres;

--
-- Name: CityInfo; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."CityInfo" (
    id integer DEFAULT nextval('public.cityrequest_id_seq'::regclass) NOT NULL,
    "DeviceInfoId" integer,
    "CityName" character varying,
    "Lat" numeric(18,12),
    "Lon" numeric(18,12),
    "FaceVersion" character varying,
    "FrameworkVersion" character varying,
    "CIQVersion" character varying,
    "RequestTime" timestamp(4) without time zone,
    "PrecipProbability" numeric,
    "RequestType" character varying,
    "Temperature" numeric,
    "Wind" numeric,
    "BaseCurrency" character varying,
    "TargetCurrency" character varying,
    "ExchangeRate" numeric,
    requestid character varying
);


ALTER TABLE public."CityInfo" OWNER TO postgres;

--
-- Name: total_devices; Type: MATERIALIZED VIEW; Schema: public; Owner: postgres
--

CREATE MATERIALIZED VIEW public.total_devices AS
 SELECT COALESCE(d."DeviceName", 'nil'::character varying) AS "DeviceName",
    count(DISTINCT d."DeviceId") AS count
   FROM (public."DeviceInfo" d
     JOIN public."CityInfo" c ON ((d.id = c."DeviceInfoId")))
  GROUP BY d."DeviceName"
  WITH NO DATA;


ALTER TABLE public.total_devices OWNER TO postgres;

--
-- Name: total_versions; Type: MATERIALIZED VIEW; Schema: public; Owner: postgres
--

CREATE MATERIALIZED VIEW public.total_versions AS
 SELECT "CityInfo"."FaceVersion" AS "Version",
    count(DISTINCT "CityInfo"."DeviceInfoId") AS "Count"
   FROM public."CityInfo"
  GROUP BY "CityInfo"."FaceVersion"
  WITH NO DATA;


ALTER TABLE public.total_versions OWNER TO postgres;

--
-- Name: uniq_month; Type: MATERIALIZED VIEW; Schema: public; Owner: postgres
--

CREATE MATERIALIZED VIEW public.uniq_month AS
 WITH "New2" AS (
         SELECT (c."RequestTime")::date AS "time",
            count(DISTINCT d.id) AS "New"
           FROM (public."DeviceInfo" d
             JOIN public."CityInfo" c ON ((d.id = c."DeviceInfoId")))
          WHERE ((d."FirstRequestTime")::date = (c."RequestTime")::date)
          GROUP BY ((c."RequestTime")::date)
        ), "Old2" AS (
         SELECT (c."RequestTime")::date AS "time",
            count(DISTINCT d.id) AS "Old"
           FROM (public."DeviceInfo" d
             JOIN public."CityInfo" c ON ((d.id = c."DeviceInfoId")))
          WHERE ((d."FirstRequestTime")::date <> (c."RequestTime")::date)
          GROUP BY ((c."RequestTime")::date)
        )
 SELECT "New2"."time",
    "New2"."New",
    "Old2"."Old",
    ("New2"."New" + "Old2"."Old") AS "Total"
   FROM ("New2"
     JOIN "Old2" ON (("New2"."time" = "Old2"."time")))
  WHERE (("New2"."time" > (now() - '1 mon'::interval)) AND ("New2"."time" < (now())::date))
  WITH NO DATA;


ALTER TABLE public.uniq_month OWNER TO postgres;

--
-- Name: view_state; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.view_state (
    id integer NOT NULL,
    refresh_time timestamp without time zone,
    view_name character varying(255)
);


ALTER TABLE public.view_state OWNER TO postgres;

--
-- Name: view_state_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.view_state ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.view_state_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: yas_route; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.yas_route (
    route_id bigint NOT NULL,
    user_id bigint NOT NULL,
    route_name character varying,
    upload_time timestamp with time zone
);


ALTER TABLE public.yas_route OWNER TO postgres;

--
-- Name: yas_route_route_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.yas_route ALTER COLUMN route_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.yas_route_route_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: yas_user; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.yas_user (
    user_id bigint NOT NULL,
    public_id character varying NOT NULL,
    telegram_id bigint NOT NULL,
    user_name character varying,
    register_time timestamp with time zone
);


ALTER TABLE public.yas_user OWNER TO postgres;

--
-- Name: yas_user_user_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.yas_user ALTER COLUMN user_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.yas_user_user_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: yas_waypoint; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.yas_waypoint (
    waypoint_id bigint NOT NULL,
    route_id bigint NOT NULL,
    waypoint_name character varying,
    lat numeric,
    lon numeric,
    order_id integer
);


ALTER TABLE public.yas_waypoint OWNER TO postgres;

--
-- Name: yas_waypoint_waypoint_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.yas_waypoint ALTER COLUMN waypoint_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.yas_waypoint_waypoint_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: DeviceInfo deviceinfo_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."DeviceInfo"
    ADD CONSTRAINT deviceinfo_pkey PRIMARY KEY (id);


--
-- Name: view_state view_state_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.view_state
    ADD CONSTRAINT view_state_pkey PRIMARY KEY (id);


--
-- Name: FK_DeviceInfoID; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "FK_DeviceInfoID" ON public."CityInfo" USING btree ("DeviceInfoId");

ALTER TABLE public."CityInfo" CLUSTER ON "FK_DeviceInfoID";


--
-- Name: IXU_DeviceID; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IXU_DeviceID" ON public."DeviceInfo" USING btree ("DeviceId");


--
-- Name: IX_DeviceId_RequestTime; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_DeviceId_RequestTime" ON public."CityInfo" USING btree ("DeviceInfoId" DESC NULLS LAST, date("RequestTime") DESC NULLS LAST);


--
-- Name: IX_RequestTime; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_RequestTime" ON public."CityInfo" USING btree ("RequestTime" DESC NULLS LAST);


--
-- Name: IX_RequestTime_Date; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_RequestTime_Date" ON public."CityInfo" USING btree (date("RequestTime"));


--
-- Name: IX_RequestTime_DeviceId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_RequestTime_DeviceId" ON public."CityInfo" USING btree (date("RequestTime") DESC NULLS LAST, "DeviceInfoId" DESC NULLS LAST);


--
-- Name: IX_Time_ID; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_Time_ID" ON public."DeviceInfo" USING btree (date("FirstRequestTime"), id);


--
-- Name: idxu_cityinfo_requestid; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX idxu_cityinfo_requestid ON public."CityInfo" USING btree (requestid);


--
-- Name: ix_firstrequest; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX ix_firstrequest ON public."DeviceInfo" USING btree ("FirstRequestTime");


--
-- Name: ix_waypoint_routeid; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX ix_waypoint_routeid ON public.yas_waypoint USING btree (route_id);

ALTER TABLE public.yas_waypoint CLUSTER ON ix_waypoint_routeid;


--
-- Name: ixu_publicid; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX ixu_publicid ON public.yas_user USING btree (public_id);

ALTER TABLE public.yas_user CLUSTER ON ixu_publicid;


--
-- Name: ixu_route_routeid; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX ixu_route_routeid ON public.yas_route USING btree (route_id);


--
-- Name: ixu_route_userid; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX ixu_route_userid ON public.yas_route USING btree (user_id);

ALTER TABLE public.yas_route CLUSTER ON ixu_route_userid;


--
-- Name: ixu_telegramid; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX ixu_telegramid ON public.yas_user USING btree (telegram_id);


--
-- Name: ixu_userid; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX ixu_userid ON public.yas_user USING btree (user_id);


--
-- Name: ixu_view_state_name; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX ixu_view_state_name ON public.view_state USING btree (view_name);


--
-- Name: ixu_waypointid; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX ixu_waypointid ON public.yas_waypoint USING btree (waypoint_id);


--
-- PostgreSQL database dump complete
--