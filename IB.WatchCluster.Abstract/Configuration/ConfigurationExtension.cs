using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace IB.WatchCluster.Abstract.Configuration
{
    public static class ConfigurationExtension
    {
        /// <summary>
        /// Load config for class TSettings from appsettings.json and validating it.
        /// The Class name is using as config section
        /// </summary>
        /// <typeparam name="TSettings">Type of settings class</typeparam>
        /// <param name="configuration">IConfiguration object<see cref="IConfiguration"/></param>
        public static TSettings LoadVerifiedConfiguration<TSettings>(this IConfiguration configuration) where TSettings : new()
            => LoadVerifiedConfiguration<TSettings, TSettings>(configuration);

        /// <summary>
        /// Load config for class TSettings from appsettings.json and validating it.
        /// The Class name is using as config section
        /// </summary>
        /// <typeparam name="ISettings">Contract of settings class</typeparam>
        /// <typeparam name="TSettings">Type of settings class</typeparam>
        /// <param name="configuration">IConfiguration object<see cref="IConfiguration"/></param>
        public static ISettings LoadVerifiedConfiguration<ISettings, TSettings>(this IConfiguration configuration)
            where TSettings : ISettings, new()
        {
            var logger = Log.Logger.ForContext<TSettings>();
            logger.Information($"validate :: { typeof(TSettings).Name }");
            try
            {
                if(!configuration.GetSection(typeof(TSettings).Name).Exists())
                    logger.Warning(
                        "Config section {@section} not found, the default object is using", 
                        typeof(TSettings).Name);
                
                var settings = new TSettings();
                configuration.Bind(typeof(TSettings).Name, settings);
                Validator.ValidateObject(settings, new ValidationContext(settings), validateAllProperties: true);
                return settings;
            }
            catch (Exception e)
            {
                logger.Error(e, $"{typeof(TSettings).Name} validation error");
                throw;
            }
        }
    }
}
