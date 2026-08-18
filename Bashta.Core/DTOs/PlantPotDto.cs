namespace Bashta.Core.DTOs;

public class PlantPotRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Location { get; set; }

    public string? MacAddress { get; set; }

    public string? FirmwareVersion { get; set; }

    public bool IsRainExposed { get; set; } = false;

    public int SensorReadingIntervalMinutes { get; set; } = 60;

    public bool? IsActive { get; set; }
}
public class PlantPotResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Location { get; set; }

    public string? MacAddress { get; set; }

    public string? FirmwareVersion { get; set; }

    public bool IsActive { get; set; }

    public bool IsRainExposed { get; set; }

    public int SensorReadingIntervalMinutes { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<PlantSummary> Plants { get; set; } = new();
}

public class PlantSummary
{
    public int Id { get; set; }

    public int PlantTypeId { get; set; }

    public string? Nickname { get; set; }

    public string PlantTypeName { get; set; } = string.Empty;

    public DateOnly PlantedAt { get; set; }
}