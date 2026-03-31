using Hangfire.Dashboard;

namespace SportsBetting.DataCollector.Api.Middleware;

/// <summary>
/// Hangfire dashboard authorization filter. Allows all access in development.
/// </summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // Allow all access — restrict in production via environment checks if needed
        return true;
    }
}
