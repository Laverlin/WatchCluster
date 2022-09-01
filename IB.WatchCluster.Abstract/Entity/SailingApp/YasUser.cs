using System;
using LinqToDB.Mapping;

namespace IB.WatchCluster.Abstract.Entity.SailingApp;

/// <summary>
/// YAS User info
/// </summary>
[Table(Name = "yas_user")]
public class YasUser
{
    [Column("user_id", IsIdentity = true)]
    public long UserId {get;set;}

    [Column("public_id")]
    public string PublicId {get;set;}

    [Column("telegram_id")]
    public long TelegramId {get;set;}

    [Column("user_name")]
    public string UserName {get;set;}

    [Column("register_time")]
    public DateTime RegisterTime {get;set;}
}