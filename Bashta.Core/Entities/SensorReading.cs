namespace Bashta.Core.Entities;

public class SensorReading
{
    public DateTime Time { get; set; } = DateTime.UtcNow;
    public int PotId { get; set; }
    public decimal? SoilMoisture { get; set; }
    public decimal? Temperature { get; set; }
    public decimal? Humidity { get; set; }
    public int? Lux { get; set; }

    // Navigation
    public PlantPot PlantPot { get; set; } = null!;
}