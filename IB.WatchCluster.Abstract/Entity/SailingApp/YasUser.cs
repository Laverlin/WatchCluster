using System;
using System.Text.Json.Serialization;
using LinqToDB.Mapping;

namespace IB.WatchCluster.Abstract.Entity.SailingApp;

/// <summary>
/// YAS User info
/// </summary>
[Table(Name = "yas_user")]
public class YasUser
{
    [Column("user_id", IsIdentity = true)]
    [JsonPropertyName("userId")]
    public long UserId {get;set;}

    [Column("public_id")]
    [JsonPropertyName("publicId")]
    public string PublicId {get; set;}

    [Column("telegram_id")]
    [JsonPropertyName("telegramId")]
    public long TelegramId {get; set;}

    [Column("user_name")]
    [JsonPropertyName("userName")]
    public string UserName {get; set;}

    [Column("register_time")]
    [JsonPropertyName("registerTime")]
    public DateTime RegisterTime {get; set;}
}