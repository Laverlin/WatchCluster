using IB.WatchCluster.Abstract.Entity.WatchFace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IB.WatchCluster.ServiceHost.Services
{
    internal class DarkSkyService : IRequestHandler<WeatherInfo>
    {
        public WeatherInfo Process(WatchRequest? watchRequest)
        {
            Task.Delay(TimeSpan.FromSeconds(20)).Wait();
            return new WeatherInfo();
        }

        public Task<WeatherInfo> ProcessAsync(WatchRequest? watchRequest)
        {
            return Task.FromResult(new WeatherInfo());
        }
    }
}
