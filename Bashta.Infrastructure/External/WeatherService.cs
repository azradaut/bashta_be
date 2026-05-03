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
        _apiKey = config["OpenWeatherMap:ApiKey"]!;
        _baseUrl = config["OpenWeatherMap:BaseUrl"]!;
        _city = config["OpenWeatherMap:City"]!;
        _countryCode = config["OpenWeatherMap:CountryCode"]!;
    }

    public async Task<WeatherResponse> GetWeatherAsync()
    {
        // Trenutno vrijeme
        var currentUrl = $"{_baseUrl}/weather?q={_city},{_countryCode}&appid={_apiKey}&units=metric";
        var currentJson = await _httpClient.GetStringAsync(currentUrl);
        var current = JsonDocument.Parse(currentJson).RootElement;

        double temp = current.GetProperty("main").GetProperty("temp").GetDouble();
        double humidity = current.GetProperty("main").GetProperty("humidity").GetDouble();
        string desc = current.GetProperty("weather")[0].GetProperty("description").GetString() ?? "";

        // Prognoza za 24h — provjeri hoće li kiše
        var forecastUrl = $"{_baseUrl}/forecast?q={_city},{_countryCode}&appid={_apiKey}&units=metric&cnt=8";
        var forecastJson = await _httpClient.GetStringAsync(forecastUrl);
        var forecast = JsonDocument.Parse(forecastJson).RootElement;

        bool rainExpected = false;
        foreach (var item in forecast.GetProperty("list").EnumerateArray())
        {
            if (item.TryGetProperty("rain", out _))
            {
                rainExpected = true;
                break;
            }

            // Provjeri i po weather ID-u (5xx = kiša)
            var weatherId = item.GetProperty("weather")[0].GetProperty("id").GetInt32();
            if (weatherId is >= 500 and <= 531)
            {
                rainExpected = true;
                break;
            }
        }

        return new WeatherResponse
        {
            RainExpectedIn24h = rainExpected,
            CurrentTemp = temp,
            CurrentHumidity = humidity,
            Description = desc
        };
    }
}