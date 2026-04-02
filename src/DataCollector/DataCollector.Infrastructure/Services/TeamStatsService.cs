using SportsBetting.DataCollector.Core.Dtos;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Clients;
using SportsBetting.DataCollector.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using SportsBetting.DataCollector.Infrastructure.Services;

namespace SportsBetting.DataCollector.Infrastructure.Services;

/// <summary>
/// Service implementation for team statistics using external APIs.
/// </summary>
public class TeamStatsService : ITeamStatsService, IScopedService
{
    private readonly SportsBettingDbContext _context;
    private readonly IFootballDataClient _footballClient;
    private readonly ILogger<TeamStatsService> _logger;

    public TeamStatsService(
        SportsBettingDbContext context,
        IFootballDataClient footballClient,
        ILogger<TeamStatsService> logger)
    {
        _context = context;
        _footballClient = footballClient;
        _logger = logger;
    }

    public async Task<TeamFormDto> GetTeamFormAsync(string teamName, string sport, CancellationToken cancellationToken = default)
    {
        if (sport.Equals("football", StringComparison.OrdinalIgnoreCase))
        {
            return await GetFootballTeamFormAsync(teamName, cancellationToken);
        }

        if (sport.Equals("tennis", StringComparison.OrdinalIgnoreCase))
        {
            return await GetTennisTeamFormFromDbAsync(teamName, cancellationToken);
        }

        return new TeamFormDto { TeamName = teamName };
    }

    public async Task<HeadToHeadDto> GetHeadToHeadAsync(string homeTeam, string awayTeam, string sport, CancellationToken cancellationToken = default)
    {
        if (sport.Equals("football", StringComparison.OrdinalIgnoreCase))
        {
            return await GetFootballHeadToHeadAsync(homeTeam, awayTeam, cancellationToken);
        }

        if (sport.Equals("tennis", StringComparison.OrdinalIgnoreCase))
        {
            return await GetTennisHeadToHeadFromDbAsync(homeTeam, awayTeam, cancellationToken);
        }

        return new HeadToHeadDto { HomeTeam = homeTeam, AwayTeam = awayTeam };
    }

    private async Task<TeamFormDto> GetTennisTeamFormFromDbAsync(string playerName, CancellationToken cancellationToken)
    {
        var playerNorm = TennisNameNormalizer.Normalize(playerName);
        var playerFuzzy = $"%{TennisNameNormalizer.SurnameToken(playerNorm)}%";

        try
        {
            var matches = await _context.Matches
                .Where(m => m.Sport == "tennis" && m.Status == "FINISHED" &&
                            m.HomeTeam != null && m.AwayTeam != null &&
                            ((m.HomeTeam.NameNormalized == playerNorm || EF.Functions.ILike(m.HomeTeam.Name, playerFuzzy)) ||
                             (m.AwayTeam.NameNormalized == playerNorm || EF.Functions.ILike(m.AwayTeam.Name, playerFuzzy))))
                .OrderByDescending(m => m.CommenceTime)
                .Take(10)
                .Select(m => new
                {
                    m.CommenceTime,
                    Home = m.HomeTeam!.Name,
                    HomeNorm = m.HomeTeam!.NameNormalized,
                    Away = m.AwayTeam!.Name,
                    m.HomeScore,
                    m.AwayScore
                })
                .ToListAsync(cancellationToken);

            if (matches.Count == 0)
            {
                // Fallback to seeded historical ELO snapshots when we don't have enough finished tennis matches
                // in the local matches table yet.
                return await GetTennisFormFromHistoricalEloAsync(playerName, cancellationToken);
            }

            var results = new List<MatchResultDto>();
            int homeScoreSum = 0, homeConcededSum = 0;
            int awayScoreSum = 0, awayConcededSum = 0;
            int homeCount = 0, awayCount = 0;

            foreach (var m in matches)
            {
                var isHome = m.HomeNorm == playerNorm ||
                             m.Home.Contains(TennisNameNormalizer.SurnameToken(playerNorm), StringComparison.OrdinalIgnoreCase);
                var opponent = isHome ? m.Away : m.Home;
                var myScore = isHome ? (m.HomeScore ?? 0) : (m.AwayScore ?? 0);
                var oppScore = isHome ? (m.AwayScore ?? 0) : (m.HomeScore ?? 0);

                // For tennis we treat score as "sets/games" aggregate stored in DB; W/L based on numeric comparison.
                var result = myScore > oppScore ? "W" : myScore < oppScore ? "L" : "D";

                if (isHome)
                {
                    homeCount++;
                    homeScoreSum += myScore;
                    homeConcededSum += oppScore;
                }
                else
                {
                    awayCount++;
                    awayScoreSum += myScore;
                    awayConcededSum += oppScore;
                }

                results.Add(new MatchResultDto
                {
                    Date = DateTime.SpecifyKind(m.CommenceTime, DateTimeKind.Utc),
                    Opponent = opponent,
                    IsHome = isHome,
                    GoalsScored = myScore,
                    GoalsConceded = oppScore,
                    Result = result
                });
            }

            return new TeamFormDto
            {
                TeamName = playerName,
                Last10Games = results,
                AvgGoalsScoredHome = homeCount > 0 ? (float)homeScoreSum / homeCount : 0,
                AvgGoalsScoredAway = awayCount > 0 ? (float)awayScoreSum / awayCount : 0,
                AvgGoalsConcededHome = homeCount > 0 ? (float)homeConcededSum / homeCount : 0,
                AvgGoalsConcededAway = awayCount > 0 ? (float)awayConcededSum / awayCount : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building tennis form from DB for {PlayerName}", playerName);
            // Last resort fallback: try seeded historical ELO.
            return await GetTennisFormFromHistoricalEloAsync(playerName, cancellationToken);
        }
    }

    private async Task<TeamFormDto> GetTennisFormFromHistoricalEloAsync(string playerName, CancellationToken cancellationToken)
    {
        try
        {
            // We use raw SQL to avoid provider translation edge-cases with pattern matching.
            // 1) Try direct contains match first.
            var pattern = $"%{playerName}%";
            var rows = await _context.PlayerEloHistory
                .FromSqlInterpolated($"""
                    SELECT player_name, surface, elo, tour, match_date, won
                    FROM player_elo_history
                    WHERE player_name ILIKE {pattern}
                    ORDER BY match_date DESC NULLS LAST
                    LIMIT 10
                    """)
                .Select(r => new { r.MatchDate, r.Won })
                .ToListAsync(cancellationToken);

            // 2) If query used abbreviations like "J. Mikulskyte", try (initial + lastname) match.
            if (rows.Count == 0)
            {
                var trimmed = playerName.Trim();
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2 && parts[0].EndsWith(".", StringComparison.Ordinal))
                {
                    var initial = parts[0].TrimEnd('.');
                    var lastName = parts[^1];
                    var abbrPattern = $"{initial}% {lastName}";

                    // Match: first letter of first name equals initial AND last token equals lastName.
                    rows = await _context.PlayerEloHistory
                        .FromSqlInterpolated($"""
                            SELECT player_name, surface, elo, tour, match_date, won
                            FROM player_elo_history
                            WHERE
                                player_name ILIKE {abbrPattern}
                            ORDER BY match_date DESC NULLS LAST
                            LIMIT 10
                            """)
                        .Select(r => new { r.MatchDate, r.Won })
                        .ToListAsync(cancellationToken);
                }
            }

            if (rows.Count == 0)
                return new TeamFormDto { TeamName = playerName };

            var results = new List<MatchResultDto>();
            foreach (var r in rows)
            {
                var date = ParseSackmannDateOrUtcNow(r.MatchDate?.ToString(CultureInfo.InvariantCulture));
                results.Add(new MatchResultDto
                {
                    Date = date,
                    Opponent = "Histórico (Sackmann)",
                    IsHome = true,
                    GoalsScored = 0,
                    GoalsConceded = 0,
                    Result = r.Won == true ? "W" : "L"
                });
            }

            // Provide simple averages as zeros; UI uses Last10Games primarily.
            return new TeamFormDto
            {
                TeamName = playerName,
                Last10Games = results,
                AvgGoalsScoredHome = 0,
                AvgGoalsScoredAway = 0,
                AvgGoalsConcededHome = 0,
                AvgGoalsConcededAway = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building tennis form from historical ELO for {PlayerName}", playerName);
            return new TeamFormDto { TeamName = playerName };
        }
    }

    private static DateTime ParseSackmannDateOrUtcNow(string? yyyymmdd)
    {
        if (!string.IsNullOrWhiteSpace(yyyymmdd) &&
            DateTime.TryParseExact(yyyymmdd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        return DateTime.UtcNow;
    }

    private async Task<HeadToHeadDto> GetTennisHeadToHeadFromDbAsync(string playerA, string playerB, CancellationToken cancellationToken)
    {
        var normA = TennisNameNormalizer.Normalize(playerA);
        var normB = TennisNameNormalizer.Normalize(playerB);
        var fuzzyA = $"%{TennisNameNormalizer.SurnameToken(normA)}%";
        var fuzzyB = $"%{TennisNameNormalizer.SurnameToken(normB)}%";

        try
        {
            var matches = await _context.Matches
                .Where(m => m.Sport == "tennis" && m.Status == "FINISHED" &&
                            m.HomeTeam != null && m.AwayTeam != null &&
                            (((m.HomeTeam.NameNormalized == normA || EF.Functions.ILike(m.HomeTeam.Name, fuzzyA)) &&
                              (m.AwayTeam.NameNormalized == normB || EF.Functions.ILike(m.AwayTeam.Name, fuzzyB))) ||
                             ((m.HomeTeam.NameNormalized == normB || EF.Functions.ILike(m.HomeTeam.Name, fuzzyB)) &&
                              (m.AwayTeam.NameNormalized == normA || EF.Functions.ILike(m.AwayTeam.Name, fuzzyA)))))
                .OrderByDescending(m => m.CommenceTime)
                .Take(10)
                .Select(m => new
                {
                    m.CommenceTime,
                    Home = m.HomeTeam!.Name,
                    HomeNorm = m.HomeTeam!.NameNormalized,
                    Away = m.AwayTeam!.Name,
                    m.HomeScore,
                    m.AwayScore
                })
                .ToListAsync(cancellationToken);

            if (matches.Count == 0)
                return new HeadToHeadDto { HomeTeam = playerA, AwayTeam = playerB };

            int aWins = 0, bWins = 0, draws = 0;
            int aPoints = 0, bPoints = 0;
            var matchResults = new List<MatchResultDto>();

            foreach (var m in matches)
            {
                var aIsHome = m.HomeNorm == normA ||
                             m.Home.Contains(TennisNameNormalizer.SurnameToken(normA), StringComparison.OrdinalIgnoreCase);
                var homeScore = m.HomeScore ?? 0;
                var awayScore = m.AwayScore ?? 0;

                // Determine winner
                if (homeScore == awayScore)
                {
                    draws++;
                }
                else
                {
                    var homeWinner = homeScore > awayScore;
                    var aWon = aIsHome ? homeWinner : !homeWinner;
                    if (aWon) aWins++; else bWins++;
                }

                aPoints += aIsHome ? homeScore : awayScore;
                bPoints += aIsHome ? awayScore : homeScore;

                matchResults.Add(new MatchResultDto
                {
                    Date = DateTime.SpecifyKind(m.CommenceTime, DateTimeKind.Utc),
                    Opponent = aIsHome ? playerB : playerA,
                    IsHome = aIsHome,
                    GoalsScored = aIsHome ? homeScore : awayScore,
                    GoalsConceded = aIsHome ? awayScore : homeScore,
                    Result = homeScore == awayScore ? "D" : (aIsHome ? (homeScore > awayScore ? "H" : "A") : (awayScore > homeScore ? "H" : "A"))
                });
            }

            return new HeadToHeadDto
            {
                HomeTeam = playerA,
                AwayTeam = playerB,
                HomeTeamWins = aWins,
                AwayTeamWins = bWins,
                Draws = draws,
                TotalMatches = matches.Count,
                HomeTeamGoals = aPoints,
                AwayTeamGoals = bPoints,
                Matches = matchResults
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building tennis H2H from DB for {PlayerA} vs {PlayerB}", playerA, playerB);
            return new HeadToHeadDto { HomeTeam = playerA, AwayTeam = playerB };
        }
    }

    private async Task<TeamFormDto> GetFootballTeamFormAsync(string teamName, CancellationToken cancellationToken)
    {
        // Teams may appear with slight naming differences across sources/UI (abbreviations, accents).
        // Use a relaxed case-insensitive match against the Teams referenced by matches.
        var team = await _context.Matches
            .Where(m =>
                (m.HomeTeam != null && EF.Functions.ILike(m.HomeTeam.Name, teamName)) ||
                (m.AwayTeam != null && EF.Functions.ILike(m.AwayTeam.Name, teamName)) ||
                (m.HomeTeam != null && EF.Functions.ILike(m.HomeTeam.Name, $"%{teamName}%")) ||
                (m.AwayTeam != null && EF.Functions.ILike(m.AwayTeam.Name, $"%{teamName}%")))
            .Select(m =>
                m.HomeTeam != null && EF.Functions.ILike(m.HomeTeam.Name, teamName)
                    ? m.HomeTeam
                    : m.AwayTeam)
            .FirstOrDefaultAsync(cancellationToken);

        if (team?.ExternalId == null)
        {
            _logger.LogWarning("Team not found in database: {TeamName}", teamName);
            return new TeamFormDto { TeamName = teamName };
        }

        try
        {
            var dateFrom = DateTime.UtcNow.AddDays(-90).ToString("yyyy-MM-dd");
            var dateTo = DateTime.UtcNow.ToString("yyyy-MM-dd");

            // football-data.org expects numeric teamId (e.g. "86"), but our DB stores ExternalId as "football-86"
            var footballTeamId = ExtractFootballDataTeamId(team.ExternalId);
            if (string.IsNullOrWhiteSpace(footballTeamId))
            {
                _logger.LogWarning("Invalid football team ExternalId: {ExternalId}", team.ExternalId);
                return new TeamFormDto { TeamName = teamName };
            }

            var response = await _footballClient.GetTeamMatchesAsync(
                footballTeamId,
                dateFrom,
                dateTo,
                cancellationToken: cancellationToken);

            if (response?.Content?.Matches == null || !response.Content.Matches.Any())
            {
                return new TeamFormDto { TeamName = teamName };
            }

            var matches = response.Content.Matches
                .Where(m => m.Status == "FINISHED" &&
                            (m.HomeTeam?.Name == teamName || m.AwayTeam?.Name == teamName))
                .OrderByDescending(m => m.UtcDate)
                .Take(10)
                .ToList();

            var results = new List<MatchResultDto>();
            int wins = 0, draws = 0, losses = 0;
            int goalsScored = 0, goalsConceded = 0;
            int homeGoalsScored = 0, homeGoalsConceded = 0;
            int awayGoalsScored = 0, awayGoalsConceded = 0;

            foreach (var match in matches)
            {
                var isHome = match.HomeTeam?.Name == teamName;
                var opponent = isHome
                    ? (match.AwayTeam?.Name ?? "Unknown")
                    : (match.HomeTeam?.Name ?? "Unknown");
                var myGoals = isHome ? match.Score?.FullTime?.Home : match.Score?.FullTime?.Away;
                var oppGoals = isHome ? match.Score?.FullTime?.Away : match.Score?.FullTime?.Home;

                var result = "D";
                if (myGoals.HasValue && oppGoals.HasValue)
                {
                    if (myGoals > oppGoals) { result = "W"; wins++; }
                    else if (myGoals < oppGoals) { result = "L"; losses++; }
                    else { draws++; }

                    goalsScored += myGoals.Value;
                    goalsConceded += oppGoals.Value;

                    if (isHome)
                    {
                        homeGoalsScored += myGoals.Value;
                        homeGoalsConceded += oppGoals.Value;
                    }
                    else
                    {
                        awayGoalsScored += myGoals.Value;
                        awayGoalsConceded += oppGoals.Value;
                    }
                }

                results.Add(new MatchResultDto
                {
                    Date = match.UtcDate ?? DateTime.MinValue,
                    Opponent = opponent ?? "Unknown",
                    IsHome = isHome,
                    GoalsScored = myGoals ?? 0,
                    GoalsConceded = oppGoals ?? 0,
                    Result = result
                });
            }

            var homeMatches = results.Where(r => r.IsHome).ToList();
            var awayMatches = results.Where(r => !r.IsHome).ToList();

            return new TeamFormDto
            {
                TeamName = teamName,
                Last10Games = results,
                AvgGoalsScoredHome = homeMatches.Any() ? (float)homeMatches.Average(r => r.GoalsScored) : 0,
                AvgGoalsScoredAway = awayMatches.Any() ? (float)awayMatches.Average(r => r.GoalsScored) : 0,
                AvgGoalsConcededHome = homeMatches.Any() ? (float)homeMatches.Average(r => r.GoalsConceded) : 0,
                AvgGoalsConcededAway = awayMatches.Any() ? (float)awayMatches.Average(r => r.GoalsConceded) : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching team form for {TeamName}", teamName);
            return new TeamFormDto { TeamName = teamName };
        }
    }

    private async Task<HeadToHeadDto> GetFootballHeadToHeadAsync(string homeTeam, string awayTeam, CancellationToken cancellationToken)
    {
        try
        {
            var fromDb = await GetFootballHeadToHeadFromHistoricalAsync(homeTeam, awayTeam, cancellationToken);
            if (fromDb.TotalMatches > 0)
                return fromDb;

            return await GetFootballHeadToHeadFromApiAsync(homeTeam, awayTeam, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building head-to-head for {HomeTeam} vs {AwayTeam}", homeTeam, awayTeam);
            return new HeadToHeadDto { HomeTeam = homeTeam, AwayTeam = awayTeam };
        }
    }

    /// <summary>
    /// Head-to-head from seeded football-data.co.uk rows. Uses flexible name matching because
    /// API/UI names (e.g. "Real Sociedad de Fútbol") differ from CSV short names ("Sociedad").
    /// Dedupes identical rows (repeated seed runs) via DISTINCT ON.
    /// </summary>
    private async Task<HeadToHeadDto> GetFootballHeadToHeadFromHistoricalAsync(
        string homeTeam,
        string awayTeam,
        CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _context.Database
                .SqlQuery<HistoricalH2hRow>($"""
                    SELECT DISTINCT ON (
                        btrim(lower(hm.home_team)),
                        btrim(lower(hm.away_team)),
                        btrim(hm.match_date),
                        hm.league_code,
                        hm.season
                    )
                        hm.id AS "Id",
                        hm.match_date AS "MatchDate",
                        hm.home_team AS "HomeTeam",
                        hm.away_team AS "AwayTeam",
                        hm.home_goals AS "HomeGoals",
                        hm.away_goals AS "AwayGoals",
                        hm.result AS "Result",
                        hm.league_name AS "LeagueName"
                    FROM historical_matches hm
                    WHERE hm.home_goals IS NOT NULL
                      AND hm.away_goals IS NOT NULL
                      AND (
                        (
                            (strpos(lower(btrim(hm.home_team)), lower(btrim({homeTeam}))) > 0
                             OR strpos(lower(btrim({homeTeam})), lower(btrim(hm.home_team))) > 0)
                            AND (strpos(lower(btrim(hm.away_team)), lower(btrim({awayTeam}))) > 0
                                 OR strpos(lower(btrim({awayTeam})), lower(btrim(hm.away_team))) > 0)
                        )
                        OR
                        (
                            (strpos(lower(btrim(hm.home_team)), lower(btrim({awayTeam}))) > 0
                             OR strpos(lower(btrim({awayTeam})), lower(btrim(hm.home_team))) > 0)
                            AND (strpos(lower(btrim(hm.away_team)), lower(btrim({homeTeam}))) > 0
                                 OR strpos(lower(btrim({homeTeam})), lower(btrim(hm.away_team))) > 0)
                        )
                      )
                    ORDER BY
                        btrim(lower(hm.home_team)),
                        btrim(lower(hm.away_team)),
                        btrim(hm.match_date),
                        hm.league_code,
                        hm.season,
                        hm.id
                    """)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
                return new HeadToHeadDto { HomeTeam = homeTeam, AwayTeam = awayTeam };

            var ordered = rows
                .Select(r => (Row: r, Dt: TryParseHistoricalMatchDate(r.MatchDate)))
                .OrderByDescending(x => x.Dt)
                .ToList();

            int homeWins = 0, awayWins = 0, draws = 0;
            int uiHomeGoals = 0, uiAwayGoals = 0;
            var matchResults = new List<MatchResultDto>();

            foreach (var (row, _) in ordered)
            {
                var isUiHomeDbHome = HistoricalNameMatches(row.HomeTeam, homeTeam)
                    && HistoricalNameMatches(row.AwayTeam, awayTeam);
                var isUiHomeDbAway = HistoricalNameMatches(row.HomeTeam, awayTeam)
                    && HistoricalNameMatches(row.AwayTeam, homeTeam);
                if (!isUiHomeDbHome && !isUiHomeDbAway)
                    continue;

                var swapped = isUiHomeDbAway;
                var hg = row.HomeGoals ?? 0;
                var ag = row.AwayGoals ?? 0;
                var uiHomeScore = swapped ? ag : hg;
                var uiAwayScore = swapped ? hg : ag;

                if (uiHomeScore == uiAwayScore)
                    draws++;
                else if (uiHomeScore > uiAwayScore)
                    homeWins++;
                else
                    awayWins++;

                uiHomeGoals += uiHomeScore;
                uiAwayGoals += uiAwayScore;

                var isHomePlaying = !swapped;
                matchResults.Add(new MatchResultDto
                {
                    Date = TryParseHistoricalMatchDate(row.MatchDate),
                    Opponent = isHomePlaying ? awayTeam : homeTeam,
                    IsHome = isHomePlaying,
                    GoalsScored = isHomePlaying ? uiHomeScore : uiAwayScore,
                    GoalsConceded = isHomePlaying ? uiAwayScore : uiHomeScore,
                    Result = uiHomeScore > uiAwayScore
                        ? "H"
                        : uiHomeScore < uiAwayScore ? "A" : "D"
                });
            }

            var totalMatches = matchResults.Count;
            matchResults = matchResults
                .OrderByDescending(m => m.Date)
                .Take(10)
                .ToList();

            return new HeadToHeadDto
            {
                HomeTeam = homeTeam,
                AwayTeam = awayTeam,
                HomeTeamWins = homeWins,
                AwayTeamWins = awayWins,
                Draws = draws,
                TotalMatches = totalMatches,
                HomeTeamGoals = uiHomeGoals,
                AwayTeamGoals = uiAwayGoals,
                Matches = matchResults
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Historical H2H query failed for {HomeTeam} vs {AwayTeam}", homeTeam, awayTeam);
            return new HeadToHeadDto { HomeTeam = homeTeam, AwayTeam = awayTeam };
        }
    }

    private static bool HistoricalNameMatches(string dbName, string uiName)
    {
        var a = dbName.Trim().ToLowerInvariant();
        var b = uiName.Trim().ToLowerInvariant();
        return a == b || a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal);
    }

    private static DateTime TryParseHistoricalMatchDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DateTime.MinValue;

        var t = raw.Trim();
        var formats = new[]
        {
            "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy",
            "yyyy-MM-dd", "dd.MM.yyyy",
        };
        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(
                    t,
                    fmt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dt))
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
        }

        if (DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var any))
            return DateTime.SpecifyKind(any.ToUniversalTime(), DateTimeKind.Utc);

        return DateTime.MinValue;
    }

    private async Task<HeadToHeadDto> GetFootballHeadToHeadFromApiAsync(
        string homeTeam,
        string awayTeam,
        CancellationToken cancellationToken)
    {
        var homeTeamEntity = await _context.Matches
            .Where(m => m.HomeTeam != null && m.HomeTeam.Name == homeTeam)
            .Select(m => m.HomeTeam)
            .FirstOrDefaultAsync(cancellationToken);

        var awayTeamEntity = await _context.Matches
            .Where(m => m.AwayTeam != null && m.AwayTeam.Name == awayTeam)
            .Select(m => m.AwayTeam)
            .FirstOrDefaultAsync(cancellationToken);

        if (homeTeamEntity?.ExternalId == null || awayTeamEntity?.ExternalId == null)
        {
            return new HeadToHeadDto { HomeTeam = homeTeam, AwayTeam = awayTeam };
        }

        var dateFrom = DateTime.UtcNow.AddYears(-2).ToString("yyyy-MM-dd");
        var dateTo = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var homeTeamId = ExtractFootballDataTeamId(homeTeamEntity.ExternalId);
        if (string.IsNullOrWhiteSpace(homeTeamId))
        {
            _logger.LogWarning("Invalid football home team ExternalId: {ExternalId}", homeTeamEntity.ExternalId);
            return new HeadToHeadDto { HomeTeam = homeTeam, AwayTeam = awayTeam };
        }

        var homeResponse = await _footballClient.GetTeamMatchesAsync(
            homeTeamId, dateFrom, dateTo, cancellationToken: cancellationToken);

        if (homeResponse?.Content?.Matches == null)
        {
            return new HeadToHeadDto { HomeTeam = homeTeam, AwayTeam = awayTeam };
        }

        bool NamesEqual(string? a, string b) =>
            !string.IsNullOrEmpty(a) &&
            string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        var h2hMatches = homeResponse.Content.Matches
            .Where(m => m.Status == "FINISHED" && m.HomeTeam?.Name != null && m.AwayTeam?.Name != null &&
                        ((NamesEqual(m.HomeTeam.Name, homeTeam) && NamesEqual(m.AwayTeam.Name, awayTeam)) ||
                         (NamesEqual(m.HomeTeam.Name, awayTeam) && NamesEqual(m.AwayTeam.Name, homeTeam))))
            .OrderByDescending(m => m.UtcDate)
            .Take(10)
            .ToList();

        int homeWins = 0, awayWins = 0, draws = 0;
        int uiHomeGoals = 0, uiAwayGoals = 0;
        var matchResults = new List<MatchResultDto>();

        foreach (var match in h2hMatches)
        {
            var isHomePlaying = NamesEqual(match.HomeTeam?.Name, homeTeam);
            var homeScore = match.Score?.FullTime?.Home ?? 0;
            var awayScore = match.Score?.FullTime?.Away ?? 0;
            var uiHomeScore = isHomePlaying ? homeScore : awayScore;
            var uiAwayScore = isHomePlaying ? awayScore : homeScore;

            string result;
            if (homeScore > awayScore)
            {
                result = isHomePlaying ? "H" : "A";
                if (isHomePlaying) homeWins++; else awayWins++;
            }
            else if (homeScore < awayScore)
            {
                result = isHomePlaying ? "A" : "H";
                if (isHomePlaying) awayWins++; else homeWins++;
            }
            else
            {
                result = "D";
                draws++;
            }

            uiHomeGoals += uiHomeScore;
            uiAwayGoals += uiAwayScore;

            matchResults.Add(new MatchResultDto
            {
                Date = match.UtcDate ?? DateTime.MinValue,
                Opponent = isHomePlaying ? awayTeam : homeTeam,
                IsHome = isHomePlaying,
                GoalsScored = isHomePlaying ? homeScore : awayScore,
                GoalsConceded = isHomePlaying ? awayScore : homeScore,
                Result = result
            });
        }

        return new HeadToHeadDto
        {
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
            HomeTeamWins = homeWins,
            AwayTeamWins = awayWins,
            Draws = draws,
            TotalMatches = h2hMatches.Count,
            HomeTeamGoals = uiHomeGoals,
            AwayTeamGoals = uiAwayGoals,
            Matches = matchResults
        };
    }

    private sealed class HistoricalH2hRow
    {
        public long Id { get; set; }
        public string MatchDate { get; set; } = string.Empty;
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;
        public int? HomeGoals { get; set; }
        public int? AwayGoals { get; set; }
        public string? Result { get; set; }
        public string? LeagueName { get; set; }
    }

    private static string ExtractFootballDataTeamId(string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return string.Empty;

        if (externalId.StartsWith("football-", StringComparison.OrdinalIgnoreCase))
            return externalId.Substring("football-".Length);

        return externalId;
    }
}
