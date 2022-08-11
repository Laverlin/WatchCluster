using LinqToDB.Data;
using IB.WatchCluster.DbSink.Configuration;

namespace IB.WatchCluster.DbSink.Infrastructure;

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
    public DataConnectionFactory(string dataProvider, string connectionString)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public DataConnectionFactory(IDbProviderConfiguration connectionSettings) :
        this(connectionSettings.GetDataProvider(), connectionSettings.BuildConnectionString())
    { }

    public string ProviderName => _dataProvider;

    public virtual DataConnection Create() => new (_dataProvider, _connectionString);
}