using Microsoft.EntityFrameworkCore;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Infrastructure.Data;
using SportsBetting.DataCollector.Infrastructure.Repositories;

namespace SportsBetting.DataCollector.Tests.Repositories;

/// <summary>
/// Garante que GetUpcomingAsync aplica a janela <c>from</c>/<c>to</c> e o filtro onlyWithOdds
/// (regressão: antes <c>from</c> era ignorado e o proxy nginx podia dar 504 em cargas maiores).
/// </summary>
public class MatchRepositoryGetUpcomingTests
{
    private static SportsBettingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SportsBettingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new SportsBettingDbContext(options);
    }

    [Fact]
    public async Task GetUpcomingAsync_Excludes_Scheduled_Match_Before_Client_From()
    {
        await using var ctx = CreateContext();
        var homeId = Guid.NewGuid();
        var awayId = Guid.NewGuid();
        ctx.Teams.AddRange(
            new TeamEntity { Id = homeId, Name = "Home FC", Sport = "football", ExternalId = "h1" },
            new TeamEntity { Id = awayId, Name = "Away FC", Sport = "football", ExternalId = "a1" });

        var windowFrom = new DateTime(2026, 4, 5, 12, 0, 0, DateTimeKind.Utc);
        var windowTo = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var asOf = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);

        ctx.Matches.AddRange(
            new MatchEntity
            {
                Id = Guid.NewGuid(),
                ExternalId = "m-old",
                Sport = "football",
                HomeId = homeId,
                AwayId = awayId,
                CommenceTime = windowFrom.AddDays(-2),
                Status = "SCHEDULED",
            },
            new MatchEntity
            {
                Id = Guid.NewGuid(),
                ExternalId = "m-ok",
                Sport = "football",
                HomeId = homeId,
                AwayId = awayId,
                CommenceTime = windowFrom.AddHours(2),
                Status = "SCHEDULED",
            });

        await ctx.SaveChangesAsync();

        var repo = new MatchRepository(ctx);
        var result = await repo.GetUpcomingAsync(
            windowFrom,
            windowTo,
            sport: "football",
            onlyWithOdds: false,
            asOfUtc: asOf,
            cancellationToken: default);

        var list = result.ToList();
        Assert.Single(list);
        Assert.Equal("m-ok", list[0].ExternalId);
    }

    [Fact]
    public async Task GetUpcomingAsync_OnlyWithOdds_Excludes_Match_Without_Odds()
    {
        await using var ctx = CreateContext();
        var homeId = Guid.NewGuid();
        var awayId = Guid.NewGuid();
        ctx.Teams.AddRange(
            new TeamEntity { Id = homeId, Name = "H", Sport = "football", ExternalId = "h2" },
            new TeamEntity { Id = awayId, Name = "A", Sport = "football", ExternalId = "a2" });

        var from = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc);
        var asOf = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);

        var withOddsId = Guid.NewGuid();
        var noOddsId = Guid.NewGuid();
        ctx.Matches.AddRange(
            new MatchEntity
            {
                Id = withOddsId,
                ExternalId = "with",
                Sport = "football",
                HomeId = homeId,
                AwayId = awayId,
                CommenceTime = from.AddDays(1),
                Status = "SCHEDULED",
            },
            new MatchEntity
            {
                Id = noOddsId,
                ExternalId = "without",
                Sport = "football",
                HomeId = homeId,
                AwayId = awayId,
                CommenceTime = from.AddDays(2),
                Status = "SCHEDULED",
            });

        ctx.Odds.Add(new OddsEntity
        {
            Id = Guid.NewGuid(),
            MatchId = withOddsId,
            Bookmaker = "test",
            Market = "1X2",
            Outcome = "1",
            OddDecimal = 2.0m,
            ImpliedProbability = 0.5m,
            CapturedAt = asOf,
        });

        await ctx.SaveChangesAsync();

        var repo = new MatchRepository(ctx);
        var result = await repo.GetUpcomingAsync(
            from,
            to,
            sport: "football",
            onlyWithOdds: true,
            asOfUtc: asOf,
            cancellationToken: default);

        var list = result.ToList();
        Assert.Single(list);
        Assert.Equal("with", list[0].ExternalId);
    }
}
