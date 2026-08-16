namespace Bashta.Core.Entities;

public class WateringEvent
{
    public int Id { get; set; }
    public int PotId { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;  // auto, manual, schedule
    public int? DurationSec { get; set; }
    public int? AmountMl { get; set; }
    public int? SoilMoistureBefore { get; set; }
    public int? SoilMoistureAfter { get; set; }
    public bool Skipped { get; set; } = false;
    public string? SkipReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsForced { get; set; } = false;

    public string? DecisionReason { get; set; }

    public string? WeatherSummary { get; set; }


    public PlantPot PlantPot { get; set; } = null!;
}