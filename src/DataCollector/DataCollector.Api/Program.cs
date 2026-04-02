using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Refit;
using SportsBetting.DataCollector.Api.Extensions;
using SportsBetting.DataCollector.Api.Middleware;
using SportsBetting.DataCollector.Api.Services;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Services;
using SportsBetting.DataCollector.Infrastructure.Clients;
using SportsBetting.DataCollector.Infrastructure.Data;

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
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
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
            ?? throw new InvalidOperationException("Connection string Postgres or DefaultConnection not found.");

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

        // Refit Clients — todos com resilience (retry + circuit breaker via Polly)
        builder.Services.AddRefitClient<IFootballDataClient>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.football-data.org/v4");
                client.DefaultRequestHeaders.Add("X-Auth-Token",
                    builder.Configuration["ApiKeys:FootballData"]!);
            })
            .AddSportsBettingResilience("FootballData");

        builder.Services.AddRefitClient<ITennisApiClient>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.api-tennis.com/tennis");
            })
            .AddSportsBettingResilience("TennisApi");

        builder.Services.AddRefitClient<IApiFootballClient>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://v3.football.api-sports.io");
                client.DefaultRequestHeaders.Add("x-apisports-key",
                    builder.Configuration["ApiKeys:ApiFootball"] ?? string.Empty);
            })
            .AddSportsBettingResilience("ApiFootball");

        builder.Services.AddTransient<OddsApiKeyHandler>();
        builder.Services.AddRefitClient<IOddsApiClient>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.the-odds-api.com/v4");
            })
            .AddHttpMessageHandler<OddsApiKeyHandler>()
            .AddSportsBettingResilience("OddsApi");

        // Message Queue
        builder.Services.AddSingleton<IMessageQueuePublisher, RabbitMqPublisher>();

        // Team Stats Service
        builder.Services.AddScoped<ITeamStatsService, TeamStatsService>();

        // Reflection-based service registration
        builder.Services.AddServicesByReflection(
            typeof(Program).Assembly,
            typeof(SportsBettingDbContext).Assembly);

        // JobDispatcher — used by Hangfire to resolve and execute IJobService implementations via DI
        builder.Services.AddTransient<JobDispatcher>();

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
                var jobInstance = (IJobService?)ActivatorUtilities.CreateInstance(scope.ServiceProvider, jobType);
                if (jobInstance == null) continue;

                // Capture type name for the Hangfire closure — AssemblyQualifiedName
                // allows JobDispatcher to resolve the concrete type at runtime via DI
                var capturedTypeName = jobType.AssemblyQualifiedName!;
                RecurringJob.AddOrUpdate<JobDispatcher>(
                    jobInstance.JobId,
                    dispatcher => dispatcher.ExecuteAsync(capturedTypeName),
                    jobInstance.CronExpression);

                Console.WriteLine($"Scheduled job: {jobInstance.JobId} ({jobType.Name}) with cron: {jobInstance.CronExpression}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to schedule job {jobType.Name}: {ex.Message}");
            }
        }
    }
}

/// <summary>
/// Custom JSON converter that serializes DateTime as UTC with Z suffix.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
            return DateTime.MinValue;

        // Try parsing with Z suffix first, then without
        if (DateTime.TryParse(value, out var result))
        {
            return DateTime.SpecifyKind(result, DateTimeKind.Utc);
        }
        
        return DateTime.MinValue;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Always write as UTC with Z suffix
        var utcValue = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        writer.WriteStringValue(utcValue.ToString("yyyy-MM-ddTHH:mm:ssZ"));
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
        request.RequestUri = new Uri($"{uri}{separator}apiKey={_apiKey}");
        return await base.SendAsync(request, cancellationToken);
    }
}
