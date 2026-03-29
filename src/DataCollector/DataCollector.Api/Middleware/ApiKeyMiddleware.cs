namespace SportsBetting.DataCollector.Api.Middleware;

/// <summary>
/// Middleware that validates API Key authentication for all requests.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    private const string ApiKeyHeader = "X-API-Key";

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        // Skip authentication for health check and Swagger
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.StartsWith("/health") ||
            path.StartsWith("/swagger") ||
            path.StartsWith("/hangfire"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var extractedApiKey))
        {
            _logger.LogWarning("API Key was not provided");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("API Key is missing");
            return;
        }

        var apiKey = configuration["InternalApiKey"];

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("Internal API Key is not configured");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Server configuration error");
            return;
        }

        if (!apiKey.Equals(extractedApiKey))
        {
            _logger.LogWarning("Invalid API Key provided");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid API Key");
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering the API Key middleware.
/// </summary>
public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiKeyMiddleware>();
    }
}
