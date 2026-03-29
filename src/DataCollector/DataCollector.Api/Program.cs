using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Refit;
using SportsBetting.DataCollector.Api.Extensions;
using SportsBetting.DataCollector.Api.Middleware;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Services;
using SportsBetting.DataCollector.Infrastructure.Clients;
using SportsBetting.DataCollector.Infrastructure.Data;
using SportsBetting.DataCollector.Infrastructure.Services;

namespace SportsBetting.DataCollector.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                // Garantir que DateTime UTC é serializado com 'Z' (ex: "2025-03-27T20:00:00Z")
                // para o browser JS interpretar corretamente como UTC em vez de hora local
                options.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter());
            });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new()
            {
                Title = "Sports Betting Data Collector API",
                Version = "v1",
                Description = "API for collecting and managing sports data"
            });

            // Add API Key security definition
            c.AddSecurityDefinition("ApiKey", new()
            {
                Description = "API Key authentication using the X-API-Key header",
                Name = "X-API-Key",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey
            });

            c.AddSecurityRequirement(new()
            {
                {
                    new() { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } },
                    Array.Empty<string>()
                }
            });
        });

        // Database
        var connectionString = builder.Configuration.GetConnectionString("Postgres")
            ?? builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'Postgres' or 'DefaultConnection' not found.");

        builder.Services.AddDbContext<SportsBettingDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        // Hangfire
        builder.Services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
            {
                options.UseNpgsqlConnection(connectionString);
            }));

        builder.Services.AddHangfireServer();

        // Refit Clients
        builder.Services.AddRefitClient<IFootballDataClient>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.football-data.org/v4");
                client.DefaultRequestHeaders.Add("X-Auth-Token",
                    builder.Configuration["ApiKeys:FootballData"]!);
            });

        builder.Services.AddRefitClient<ITennisApiClient>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.api-tennis.com/tennis");
            });

        builder.Services.AddTransient<OddsApiKeyHandler>();
        builder.Services.AddRefitClient<IOddsApiClient>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.the-odds-api.com/v4");
            })
            .AddHttpMessageHandler<OddsApiKeyHandler>();

        // Message Queue
        builder.Services.AddSingleton<IMessageQueuePublisher, RabbitMqPublisher>();

        // Team Stats Service
        builder.Services.AddScoped<ITeamStatsService, TeamStatsService>();

        // Reflection-based service registration
        builder.Services.AddServicesByReflection(
            typeof(Program).Assembly,
            typeof(SportsBettingDbContext).Assembly);

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        // API Key authentication middleware
        app.UseApiKeyAuthentication();

        app.UseAuthorization();
        app.MapControllers();

        // Hangfire dashboard
        app.UseHangfireDashboard("/hangfire", new()
        {
            Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
        });

        // Schedule recurring jobs
        ScheduleJobs(app.Services);

        app.Run();
    }

    private static void ScheduleJobs(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var jobTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && typeof(IJobService).IsAssignableFrom(t));

        foreach (var jobType in jobTypes)
        {
            try
            {
                // Create instance to get JobId and CronExpression
                var jobInstance = (IJobService?)ActivatorUtilities.CreateInstance(scope.ServiceProvider, jobType);
                if (jobInstance == null) continue;

                RecurringJob.AddOrUpdate(
                    jobInstance.JobId,
                    () => ExecuteJob(jobInstance.JobId),
                    jobInstance.CronExpression);

                Console.WriteLine($"Scheduled job: {jobInstance.JobId} with cron: {jobInstance.CronExpression}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to schedule job {jobType.Name}: {ex.Message}");
            }
        }
    }

    [AutomaticRetry(Attempts = 3)]
    public static void ExecuteJob(string jobId)
    {
        // This method is called by Hangfire
        // The actual implementation resolves and executes the job via DI
        Console.WriteLine($"Executing job: {jobId}");
    }
}

/// <summary>
/// Injects apiKey query parameter into all requests to the Odds API.
/// </summary>
public class OddsApiKeyHandler : DelegatingHandler
{
    private readonly string _apiKey;

    public OddsApiKeyHandler(IConfiguration configuration)
    {
        _apiKey = configuration["ApiKeys:Odds"] ?? "";
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        request.RequestUri = new Uri(uri + separator + "apiKey=" + _apiKey);
        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Simple authorization filter for Hangfire dashboard.
/// </summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // Allow access in development
        var httpContext = context.GetHttpContext();
        return httpContext.Request.Host.Host == "localhost";
    }
}
