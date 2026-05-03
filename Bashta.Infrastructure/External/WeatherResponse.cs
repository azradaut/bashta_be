namespace Bashta.Infrastructure.External;

public class WeatherResponse
{
    public bool RainExpectedIn24h { get; set; }
    public double CurrentTemp { get; set; }
    public double CurrentHumidity { get; set; }
    public string Description { get; set; } = string.Empty;
}