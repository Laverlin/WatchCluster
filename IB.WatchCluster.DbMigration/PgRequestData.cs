using LinqToDB.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IB.WatchCluster.DbMigration
{
    /// <summary>
    /// Represents request and response data for the storage
    /// </summary>
    [Table("CityInfo")]
    public class PgRequestData
    {
        [Column("id"), Identity]
        public long? Id { get; set; }

        [Column("DeviceInfoId")]
        public int? DeviceDataId { get; set; }

        [Column("RequestTime")]
        public DateTime? RequestTime { get; set; }

        [Column("CityName")]
        public string CityName { get; set; }

        [Column("Lat")]
        public decimal? Lat { get; set; }

        [Column("Lon")]
        public decimal? Lon { get; set; }

        [Column("FaceVersion")]
        public string Version { get; set; }

        [Column("FrameworkVersion")]
        public string Framework { get; set; }

        [Column("CIQVersion")]
        public string CiqVersion { get; set; }

        [Column("RequestType")]
        public RequestType RequestType { get; set; }

        [Column("Temperature")]
        public decimal Temperature { get; set; }

        [Column("Wind")]
        public decimal WindSpeed { get; set; }

        [Column("PrecipProbability")]
        public decimal PrecipProbability { get; set; }

        [Column("BaseCurrency")]
        public string BaseCurrency { get; set; }

        [Column("TargetCurrency")]
        public string TargetCurrency { get; set; }

        [Column("ExchangeRate")]
        public decimal ExchangeRate { get; set; }
    }

    public enum RequestType
    {
        [MapValue("Location")]
        Location = 0,

        [MapValue("Weather")]
        Weather = 1,

        [MapValue("ExchangeRate")]
        ExchangeRate = 2
    }
}
