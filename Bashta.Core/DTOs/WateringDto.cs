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
    public DateTime CreatedAt { get; set; }
}

public class ManualWateringRequest
{
    public int PotId { get; set; }
    public int DurationSec { get; set; }
    public int? AmountMl { get; set; }
}