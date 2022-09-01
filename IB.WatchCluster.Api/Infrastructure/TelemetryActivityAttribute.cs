using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IB.WatchCluster.Api.Infrastructure;

[AttributeUsage(AttributeTargets.Method)]
public class TelemetryActivityAttribute: Attribute, IFilterFactory
{
    private readonly string _sourceName;

    public TelemetryActivityAttribute(string sourceName)
    {
        _sourceName = sourceName;
    }
    
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var activitySource = serviceProvider.GetRequiredService<ActivitySource>();
        return new TelemetryActivity(activitySource, _sourceName);
    }

    public bool IsReusable => false;

    private class TelemetryActivity: IAsyncActionFilter
    {
        private readonly ActivitySource _activitySource;
        private readonly string _sourceName;

        public TelemetryActivity(ActivitySource activitySource, string sourceName)
        {
            _activitySource = activitySource;
            _sourceName = sourceName;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            using (_activitySource.StartActivity(_sourceName))
            {
                await next();
            }
        }
    }
}