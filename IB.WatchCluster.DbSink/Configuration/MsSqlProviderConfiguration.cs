using LinqToDB.DataProvider.SqlServer;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using IB.WatchCluster.Abstract.Database;


namespace IB.WatchCluster.DbSink.Configuration
{
    public class MsSqlProviderConfiguration : IDbProviderConfiguration
    {
        /// <summary>
        /// Return new instance of <see cref="SqlServerDataProvider"/>
        /// </summary>
        public string GetDataProvider()
        {
            //var sqlProvider = SqlServerTools.GetDataProvider(SqlServerVersion.v2017);
            return LinqToDB.ProviderName.SqlServer2019;
        }

        /// <summary>
        /// Server name
        /// </summary>
        [Required]
        public string Server { get; set; } = default!;

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

        public bool TrustServerCertificate { get; set; } = true;
    }
}
