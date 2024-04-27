using System.Diagnostics;

namespace LantanaGroup.Link.Shared.Application.Extensions.Telemetry
{
    public static class ActvityExtensions
    {
        public static Activity? StartActivityWithTags(this ActivitySource activitySource, string activityName, List<KeyValuePair<string, object?>> tags)
        {
            return activitySource.StartActivity(activityName, 
                ActivityKind.Internal, 
                Activity.Current?.Context ?? new ActivityContext(),
                tags);
        }
    }
}
