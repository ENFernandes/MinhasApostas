using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Core.Entities;

/// <summary>
/// Represents a football team.
/// </summary>
public class TeamEntity
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Sport { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Country { get; set; }
    public Guid? CompetitionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
