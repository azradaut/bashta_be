using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Bashta.Infrastructure.External;

public class WeatherService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _city;
    private readonly string _countryCode;

    public WeatherService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _apiKey = config["OpenWeatherMap:ApiKey"] ?? string.Empty;
        _baseUrl = config["OpenWeatherMap:BaseUrl"] ?? "https://api.openweathermap.org/data/2.5";
        _city = config["OpenWeatherMap:City"] ?? "Sarajevo";
        _countryCode = config["OpenWeatherMap:CountryCode"] ?? "BA";
    }

    public async Task<WeatherResponse> GetWeatherAsync()
    {
        var response = new WeatherResponse
        {
            City = $"{_city}, {_countryCode}"
        };

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            response.IsAvailable = false;
            response.ErrorMessage = "OpenWeatherMap API ključ nije konfigurisan.";
            response.Description = "Vremenska prognoza trenutno nije dostupna.";
            return response;
        }

        try
        {
            await LoadCurrentWeatherAsync(response);
            await LoadForecastAsync(response);

            return response;
        }
        catch (Exception ex)
        {
            return new WeatherResponse
            {
                IsAvailable = false,
                City = $"{_city}, {_countryCode}",
                ErrorMessage = ex.Message,
                Description = "Vremenska prognoza trenutno nije dostupna."
            };
        }
    }

    private async Task LoadCurrentWeatherAsync(WeatherResponse response)
    {
        var currentUrl =
            $"{_baseUrl}/weather?q={_city},{_countryCode}&appid={_apiKey}&units=metric";

        var currentJson = await _httpClient.GetStringAsync(currentUrl);

        using var currentDocument = JsonDocument.Parse(currentJson);
        var current = currentDocument.RootElement;

        response.CurrentTemp = ToDecimal(
            current.GetProperty("main").GetProperty("temp").GetDouble());

        response.CurrentHumidity = ToDecimal(
            current.GetProperty("main").GetProperty("humidity").GetDouble());

        response.Description =
            current.GetProperty("weather")[0].GetProperty("description").GetString()
            ?? string.Empty;
    }

    private async Task LoadForecastAsync(WeatherResponse response)
    {
        
        // OpenWeatherMap forecast endpoint vraća prognozu u intervalima od 3 sata (3hx8=24h
         
        var forecastUrl =
            $"{_baseUrl}/forecast?q={_city},{_countryCode}&appid={_apiKey}&units=metric&cnt=8";

        var forecastJson = await _httpClient.GetStringAsync(forecastUrl);

        using var forecastDocument = JsonDocument.Parse(forecastJson);
        var forecast = forecastDocument.RootElement;

        decimal totalRainMm = 0;
        decimal minTemp = decimal.MaxValue;
        decimal maxTemp = decimal.MinValue;
        var rainDetected = false;

        foreach (var item in forecast.GetProperty("list").EnumerateArray())
        {
            var timestamp = item.GetProperty("dt").GetInt64();

            var forecastTimeUtc =
                DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;

            var temperature = ToDecimal(
                item.GetProperty("main").GetProperty("temp").GetDouble());

            minTemp = Math.Min(minTemp, temperature);
            maxTemp = Math.Max(maxTemp, temperature);

            var weather = item.GetProperty("weather")[0];
            var weatherId = weather.GetProperty("id").GetInt32();
            var description = weather.GetProperty("description").GetString() ?? string.Empty;

            var rainMm = ReadRainAmount(item);

            
                //Ako API ne vrati konretnu količinu kiše, ali weatherId pripada kiši, prati se minimalna očekivana kol. kiše da se odluka može prilagoditi.
            
            if (weatherId is >= 500 and <= 531)
            {
                rainDetected = true;

                if (rainMm == 0)
                    rainMm = 0.1m;
            }

            totalRainMm += rainMm;

            response.ForecastItems.Add(new WeatherForecastItem
            {
                ForecastTimeUtc = forecastTimeUtc,
                Temperature = temperature,
                RainMm = rainMm,
                WeatherId = weatherId,
                Description = description
            });
        }

        if (minTemp == decimal.MaxValue)
            minTemp = response.CurrentTemp;

        if (maxTemp == decimal.MinValue)
            maxTemp = response.CurrentTemp;

        response.RainAmountNext24hMm = Math.Round(totalRainMm, 1);
        response.RainExpectedIn24h = rainDetected || response.RainAmountNext24hMm > 0;
        response.RainIntensity = ClassifyRainIntensity(response.RainAmountNext24hMm);
        response.MinTempNext24h = minTemp;
        response.MaxTempNext24h = maxTemp;
        response.HeatRiskNext24h = maxTemp >= 30m;
    }

    private static decimal ReadRainAmount(JsonElement forecastItem)
    {
        if (!forecastItem.TryGetProperty("rain", out var rainElement))
            return 0m;

        if (rainElement.TryGetProperty("3h", out var rain3h))
            return ToDecimal(rain3h.GetDouble());

        if (rainElement.TryGetProperty("1h", out var rain1h))
            return ToDecimal(rain1h.GetDouble());

        return 0m;
    }

    private static string ClassifyRainIntensity(decimal rainAmountMm)
    {
        if (rainAmountMm <= 0)
            return "none";

        if (rainAmountMm < 1)
            return "very_light";

        if (rainAmountMm < 3)
            return "light";

        if (rainAmountMm < 7)
            return "moderate";

        return "heavy";
    }

    private static decimal ToDecimal(double value)
    {
        return Math.Round((decimal)value, 1);
    }
}