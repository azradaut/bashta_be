namespace Bashta.Core.Entities;

public class PlantPot
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? MacAddress { get; set; }
    public string? FirmwareVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Plant> Plants { get; set; } = new List<Plant>();
    public ICollection<WateringEvent> WateringEvents { get; set; } = new List<WateringEvent>();
    public ICollection<SensorReading> SensorReadings { get; set; } = new List<SensorReading>();
}