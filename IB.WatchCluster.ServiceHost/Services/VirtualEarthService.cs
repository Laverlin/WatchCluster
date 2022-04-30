using IB.WatchCluster.Abstract.Entity.WatchFace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IB.WatchCluster.ServiceHost.Services
{
    internal class VirtualEarthService : IRequestHandler<LocationInfo>
    {
        public LocationInfo Process(WatchRequest? watchRequest)
        {
            return new LocationInfo();
        }
    }
}
