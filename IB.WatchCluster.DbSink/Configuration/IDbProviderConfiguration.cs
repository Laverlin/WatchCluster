using LinqToDB.DataProvider;

namespace IB.WatchCluster.DbSink.Configuration
{
    /// <summary>
    /// The contract for Connection providers
    /// </summary>
    public interface IDbProviderConfiguration
    {
        /// <summary>
        /// The instance of <see cref="IDataProvider"/>
        /// </summary>
        /// <returns>The instance of <see cref="IDataProvider"/></returns>
        public string GetDataProvider();
    }
}
