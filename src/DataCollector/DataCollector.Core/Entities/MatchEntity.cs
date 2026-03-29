using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Core.Entities;

/// <summary>
/// Represents a match (football or tennis).
/// </summary>
public class MatchEntity
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Sport { get; set; } = string.Empty; // football, tennis
    public Guid? CompetitionId { get; set; }
    public Guid HomeId { get; set; }
    public Guid AwayId { get; set; }
    public DateTime CommenceTime { get; set; }
    public string Status { get; set; } = string.Empty; // SCHEDULED, LIVE, FINISHED
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public float? HomeXg { get; set; }
    public float? AwayXg { get; set; }
    public int? Minute { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public CompetitionEntity? Competition { get; set; }
    public TeamEntity? HomeTeam { get; set; }
    public TeamEntity? AwayTeam { get; set; }
}
