using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Core.Entities;

/// <summary>
/// Represents a competition (league or tournament).
/// </summary>
public class CompetitionEntity
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sport { get; set; } = string.Empty; // football, tennis
    public string? Country { get; set; }
    public string? Season { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Not mapped to database
    public bool IsActive { get; set; } = true;
}
