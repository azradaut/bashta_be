namespace Bashta.Core.Entities;

public class Recommendation
{
    public int Id { get; set; }
    public int PlantId { get; set; }
    public string Type { get; set; } = string.Empty;     // watering, disease, relocation, lighting, general
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public string Source { get; set; } = "rule_engine";  // rule_engine, ai, weather
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Plant Plant { get; set; } = null!;
}