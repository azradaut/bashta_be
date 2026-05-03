namespace Bashta.Core.DTOs;

public class PlantRequest
{
    public int PotId { get; set; }
    public int PlantTypeId { get; set; }
    public string? Nickname { get; set; }
    public DateOnly? PlantedAt { get; set; }
    public string? Notes { get; set; }
}

public class PlantResponse
{
    public int Id { get; set; }
    public int PotId { get; set; }
    public string? Nickname { get; set; }
    public DateOnly PlantedAt { get; set; }
    public string? Notes { get; set; }
    public PlantTypeDetail PlantType { get; set; } = null!;
}

public class PlantTypeDetail
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameLocal { get; set; }
    public int MinSoilMoisture { get; set; }
    public int MaxSoilMoisture { get; set; }
    public decimal MinTemp { get; set; }
    public decimal MaxTemp { get; set; }
    public decimal TargetDli { get; set; }
}