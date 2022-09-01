using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using LinqToDB.DataProvider.PostgreSQL;

namespace IB.WatchCluster.Abstract.Database;

/// <summary>
/// Implementation of <see cref="IDbProviderConfiguration"/> for Postgres 
/// </summary>
public class PgProviderConfiguration : IDbProviderConfiguration
{
    /// <summary>
    /// Return new instance of <see cref="PostgreSQLDataProvider"/>
    /// </summary>
    public string GetDataProvider()
    {
        return LinqToDB.ProviderName.PostgreSQL95;
    }

    /// <summary>
    /// Server name
    /// </summary>
    [Required]
    public string Server { get; set; } = default!;

    /// <summary>
    /// Server port, default 5432 
    /// </summary>
    public string Port { get; set; } = "5432";

    /// <summary>
    /// Database name
    /// </summary>
    [Required]
    public string Database { get; set; } = default!;

    /// <summary>
    /// Authorized user id
    /// </summary>
    [Required]
    [DisplayName("User Id")]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// User password
    /// </summary>
    [Required]
    public string Password { get; set; } = default!;

    /// <summary>
    /// Is pooling enabled
    /// </summary>
    public bool? Pooling { get; set; } = true;

    /// <summary>
    /// pool size minimum
    /// </summary>
    public int MinPoolSize { get; set; } = 10;

    /// <summary>
    /// Pool size maximum
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;
}