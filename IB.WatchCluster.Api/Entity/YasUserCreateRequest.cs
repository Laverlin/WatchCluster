using System.ComponentModel.DataAnnotations;

namespace IB.WatchCluster.Api.Entity;

public class YasUserCreateRequest
{
    [Required] 
    public long TelegramId { get; set; } = default!;

    public string UserName { get; set; } = default!;
}