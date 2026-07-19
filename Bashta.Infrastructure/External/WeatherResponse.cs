namespace Bashta.Infrastructure.External;

public class WeatherResponse
{
    public bool IsAvailable { get; set; } = true;

    public string? ErrorMessage { get; set; }

    public string City { get; set; } = string.Empty;

    public bool RainExpectedIn24h { get; set; }

    public decimal RainAmountNext24hMm { get; set; }

    public string RainIntensity { get; set; } = "none";

    public decimal CurrentTemp { get; set; }

    public decimal CurrentHumidity { get; set; }

    public decimal MinTempNext24h { get; set; }

    public decimal MaxTempNext24h { get; set; }

    public bool HeatRiskNext24h { get; set; }

    public string Description { get; set; } = string.Empty;

    public List<WeatherForecastItem> ForecastItems { get; set; } = new();
}

public class WeatherForecastItem
{
    public DateTime ForecastTimeUtc { get; set; }

    public decimal Temperature { get; set; }

    public decimal RainMm { get; set; }

    public int WeatherId { get; set; }

    public string Description { get; set; } = string.Empty;
}