using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Core.Entities;

/// <summary>
/// Represents a tennis player.
/// </summary>
public class PlayerEntity
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Sport { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public Guid? TeamId { get; set; }
    public int? Ranking { get; set; }
    public float? EloOverall { get; set; }
    public float? EloClay { get; set; }
    public float? EloGrass { get; set; }
    public float? EloHard { get; set; }
    public float? EloIndoor { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
