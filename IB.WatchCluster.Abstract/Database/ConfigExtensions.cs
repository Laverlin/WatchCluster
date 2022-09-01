using System;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace IB.WatchCluster.Abstract.Database;

/// <summary>
/// Helper class to get data from configuration object
/// </summary>
public static class ConfigExtensions
{
    /// <summary>
    /// Cache connection string builder result
    /// </summary>
    private static Lazy<string> _connectionString = default!;

    /// <summary>
    /// Build connection string by combining all public properties in string in format "name=value;..."
    /// If property name needs to be different you need to use the DisplayName Attribute
    /// </summary>
    /// <returns>Connection String</returns>
    public static string BuildConnectionString(this IDbProviderConfiguration connectionSettings)
    {
        _connectionString = new Lazy<string>(() =>
        {
            var connectionString = new StringBuilder();
            foreach (var propertyInfo in connectionSettings.GetType().GetProperties())
            {
                var value = propertyInfo.GetValue(connectionSettings);
                if (value != null)
                {
                    var name = propertyInfo.GetCustomAttribute<DisplayNameAttribute>() != null
                        ? propertyInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
                        : propertyInfo.Name;
                    connectionString.Append($"{name}={value};");
                }
            }

            return connectionString.ToString();
        });

        return _connectionString.Value;
    }

    /// <summary>
    /// creates new connection factory based on provided configuration
    /// </summary>
    public static DataConnectionFactory ConnectionFactory(this IDbProviderConfiguration dbProviderConfiguration)
    {
        return new DataConnectionFactory(dbProviderConfiguration);
    }
}