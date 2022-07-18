using IB.WatchCluster.Abstract;
using IB.WatchCluster.Abstract.Configuration;
using IB.WatchCluster.DbSink.Configuration;
using IB.WatchCluster.DbSink.Infrastructure;
using Microsoft.Extensions.Configuration;
using LinqToDB;
using Serilog;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.DbMigration;
using LinqToDB.Data;
using System.Reflection;

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .Build();

var logger = new LoggerConfiguration()
  .ReadFrom.Configuration(config)
  .Enrich.FromLogContext()
  .Enrich.WithProperty("version", SolutionInfo.Version)
  .Enrich.WithProperty("Application", SolutionInfo.Name)
  .CreateLogger();
Log.Logger = logger;

try
{
    var startTime = DateTime.Now;
    Log.Information("Starting the migration service :: {@date}", startTime);


    var pgProvider = config.LoadVerifiedConfiguration<PgProviderConfiguration>();
    var pgConnectionFactory = new DataConnectionFactory(pgProvider);
    var msProvider = config.LoadVerifiedConfiguration<MsSqlProviderConfiguration>();
    var msConnectionFactory = new DataConnectionFactory(msProvider);

    using var pgDb = pgConnectionFactory.Create();
    using var msDb = msConnectionFactory.Create();

    var devices = await pgDb.GetTable<DeviceData>().ToArrayAsync();
    var deviceCount = devices.Count();

    Log.Information("Load {@count} devices", deviceCount);

    var count = 0;
    foreach (var device in devices)
    {
        count++;
        var records = await pgDb
            .GetTable<PgRequestData>()
            .Where(rd => rd.DeviceDataId == device.Id)
            .Select(r => new ProcessingLog()
            {
                Id = r.Id,
                DeviceDataId = device.Id,
                Lat = r.Lat,
                Lon = r.Lon,
                CiqVersion = r.CiqVersion,
                CityName = r.CityName,
                BaseCurrency = r.BaseCurrency,
                ExchangeRate = r.ExchangeRate,
                Framework = r.Framework,
                PrecipProbability = r.PrecipProbability,
                RequestTime = r.RequestTime,
                TargetCurrency = r.TargetCurrency,
                Temperature = r.Temperature,
                Version = r.Version,
                WindSpeed = r.WindSpeed,
            }).ToArrayAsync();


        await msDb.GetTable<DeviceData>().DataContext.InsertAsync(device);
        await msDb.GetTable<ProcessingLog>()
            .BulkCopyAsync(new BulkCopyOptions { KeepIdentity = true }, records);

        Log.Information("{@count} out of {@total} devices processed - {@percent}% :: current: {@current}, requests num:{@requests}                           ",
                        count, devices.Length, count * 100 / devices.Length, device.DeviceName, records.Length);
    }

    Log.Information("Done. Duration :: {@duration}", DateTime.Now - startTime);
}
catch (Exception ex)
{
    Log.Error(ex, "Error migrating data");
}
