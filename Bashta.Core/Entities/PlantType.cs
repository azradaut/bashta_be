namespace Bashta.Core.Entities;

public class PlantType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameLocal { get; set; }
    public int MinSoilMoisture { get; set; }
    public int MaxSoilMoisture { get; set; }
    public decimal MinTemp { get; set; }
    public decimal MaxTemp { get; set; }
    public int MinHumidity { get; set; }
    public int MaxHumidity { get; set; }
    public int MinLux { get; set; }
    public decimal TargetDli { get; set; }
    public int WateringIntervalHours { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public ICollection<Plant> Plants { get; set; } = new List<Plant>();
}