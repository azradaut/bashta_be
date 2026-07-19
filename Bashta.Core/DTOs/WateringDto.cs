namespace Bashta.Core.DTOs;

public class WateringEventResponse
{
    public int Id { get; set; }

    public int PotId { get; set; }

    public string TriggeredBy { get; set; } = string.Empty;

    public int? DurationSec { get; set; }

    public int? AmountMl { get; set; }

    public int? SoilMoistureBefore { get; set; }

    public int? SoilMoistureAfter { get; set; }

    public bool Skipped { get; set; }

    public string? SkipReason { get; set; }

    public bool IsForced { get; set; }

    public string? DecisionReason { get; set; }

    public string? WeatherSummary { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class ManualWateringRequest
{
    public int PotId { get; set; }

    public int DurationSec { get; set; } = 10;

    public int? AmountMl { get; set; }

    public bool Force { get; set; } = false;
}

public class WateringStatusResponse
{
    public int PotId { get; set; }

    public int? PlantId { get; set; }

    public string? PlantName { get; set; }

    public decimal? CurrentSoilMoisture { get; set; }

    public DateTime? LastSensorReadingAt { get; set; }

    public DateTime? NextExpectedSensorReadingAt { get; set; }

    public int? MinRecommendedSoilMoisture { get; set; }

    public int? MaxRecommendedSoilMoisture { get; set; }

    public DateTime? LastWateredAt { get; set; }

    public int WateringCountLast24h { get; set; }

    public int MaxWateringCountLast24h { get; set; } = 2;

    public int RemainingWateringsLast24h { get; set; }

    public int RecommendedAmountMl { get; set; }

    public bool CanWater { get; set; }

    public bool IsWateringRecommended { get; set; }

    public bool RequiresForce { get; set; }

    public bool IsAutomaticWateringAllowedNow { get; set; }

    public string NextRecommendedWateringWindow { get; set; } = string.Empty;

    public string StatusMessage { get; set; } = string.Empty;

    public string? WarningMessage { get; set; }

    public bool IsRainExposed { get; set; }

    public bool WeatherAvailable { get; set; }

    public string WeatherSummary { get; set; } = string.Empty;

    public bool RainExpectedIn24h { get; set; }

    public decimal RainAmountNext24hMm { get; set; }

    public string RainIntensity { get; set; } = "none";

    public decimal? CurrentOutdoorTemperature { get; set; }

    public decimal? MaxTemperatureNext24h { get; set; }

    public bool HeatRiskNext24h { get; set; }

    public string? WeatherImpactMessage { get; set; }

    public List<WateringEventResponse> RecentEvents { get; set; } = new();
}