using IB.WatchCluster.Abstract.Entity.WatchFace;


namespace IB.WatchCluster.ServiceHost.Services
{
    public interface IRequestHandler<T>
    {
        public T Process(WatchRequest? watchRequest);
    }
}
