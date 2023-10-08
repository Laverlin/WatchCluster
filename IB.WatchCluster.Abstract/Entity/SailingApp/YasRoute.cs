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
    [JsonPropertyName("RouteId")]
    public long RouteId { get; set; } 

    [Column("user_id")]
    [JsonPropertyName("UserId")]
    public long UserId { get; set; }

    [Column("route_name")]
    [JsonPropertyName("RouteName")]
    public string RouteName { get; set; }

    [Column("upload_time")]
    [JsonPropertyName("RouteDate")]
    public DateTime UploadTime { get; set; }

    [JsonPropertyName("WayPoints")]
    public YasWaypoint[] Waypoints { get; set; }
}