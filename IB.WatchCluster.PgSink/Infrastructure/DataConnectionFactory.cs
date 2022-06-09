using System.Diagnostics.CodeAnalysis;
using LinqToDB.Data;
using IB.WatchCluster.DbSink.Configuration;

namespace IB.WatchCluster.DbSink.Infrastructure
{
    /// <summary>
    /// Factory to work with Data Connection from DI 
    /// </summary>
    public class DataConnectionFactory
    {
        private readonly string _dataProvider;
        private readonly string _connectionString;

        /// <summary>
        /// Store parameters for Create
        /// </summary>
        /// <param name="dataProvider">Data provider entity</param>
        /// <param name="connectionString">Connection string</param>
        public DataConnectionFactory([NotNull] string dataProvider, [NotNull] string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));

            _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
            _connectionString = connectionString;
        }

        public DataConnectionFactory(IDbProviderConfiguration connectionSettings) :
            this(connectionSettings.GetDataProvider(), connectionSettings.BuildConnectionString())
        { }

        public string ProviderName { get { return _dataProvider; } }

        public virtual DataConnection Create()
        {
            return new DataConnection(_dataProvider, _connectionString);
        }
    }
}
