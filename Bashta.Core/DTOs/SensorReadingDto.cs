namespace Bashta.Core.DTOs;

public class SensorReadingRequest
{
    public int PotId { get; set; }
    public decimal? SoilMoisture { get; set; }
    public decimal? Temperature { get; set; }
    public decimal? Humidity { get; set; }
    public int? Lux { get; set; }
    public decimal? WaterLevel { get; set; }
}

public class SensorReadingResponse
{
    public DateTime Time { get; set; }
    public int PotId { get; set; }
    public decimal? SoilMoisture { get; set; }
    public decimal? Temperature { get; set; }
    public decimal? Humidity { get; set; }
    public int? Lux { get; set; }
    public decimal? WaterLevel { get; set; }
}

public class SensorHistoryRequest
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}