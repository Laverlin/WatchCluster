using System;
using System.Linq;
using System.Text.Json.Serialization;
using LinqToDB.Mapping;

namespace IB.WatchCluster.Abstract.Entity.SailingApp;

/// <summary>
/// Route Information
/// </summary>
[Table("yas_route")]
public class YasRoute
{
    [Column("route_id", IsIdentity = true)]
    [JsonPropertyName("routeId")]
    public long RouteId { get; set; } 

    [Column("user_id")]
    [JsonPropertyName("userId")]
    public long UserId { get; set; }

    [Column("route_name")]
    [JsonPropertyName("routeName")]
    public string RouteName { get; set; }

    [Column("upload_time")]
    [JsonPropertyName("routeDate")]
    public DateTime UploadTime { get; set; }

    [JsonPropertyName("waypoints")]
    public YasWaypoint[] Waypoints { get; set; }
}