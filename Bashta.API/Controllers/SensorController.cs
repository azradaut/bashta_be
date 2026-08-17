using Bashta.Core.DTOs;
using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Core.Services;
using Bashta.Infrastructure.External;
using Microsoft.AspNetCore.Mvc;
using System.Timers;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SensorController : ControllerBase
{
    private readonly ISensorReadingRepository _sensorRepo;
    private readonly IPlantRepository _plantRepo;
    private readonly IWateringEventRepository _wateringRepo;
    private readonly IDiseaseDetectionRepository _diseaseDetectionRepo;
    private readonly IDiseaseRepository _diseaseRepo;
    private readonly IRecommendationRepository _recommendationRepo;
    private readonly INotificationRepository _notificationRepo;
    private readonly WateringRuleEngine _ruleEngine;
    private readonly DLIService _dliService;
    private readonly RecommendationService _recommendationService;
    private readonly IPlantPotRepository _potRepo;
    private readonly WeatherService _weatherService;

    public SensorController(
        ISensorReadingRepository sensorRepo,
        IPlantRepository plantRepo,
        IPlantPotRepository potRepo,
        IWateringEventRepository wateringRepo,
        IDiseaseDetectionRepository diseaseDetectionRepo,
        IDiseaseRepository diseaseRepo,
        IRecommendationRepository recommendationRepo,
        INotificationRepository notificationRepo,
        WateringRuleEngine ruleEngine,
        DLIService dliService,
        RecommendationService recommendationService,
        WeatherService weatherService)
        {
            _sensorRepo = sensorRepo;
            _plantRepo = plantRepo;
            _potRepo = potRepo;
            _wateringRepo = wateringRepo;
            _diseaseDetectionRepo = diseaseDetectionRepo;
            _diseaseRepo = diseaseRepo;
            _recommendationRepo = recommendationRepo;
            _notificationRepo = notificationRepo;
            _ruleEngine = ruleEngine;
            _dliService = dliService;
            _recommendationService = recommendationService;
            _weatherService = weatherService;
        }

    [HttpGet("{potId}/latest")]
    public async Task<IActionResult> GetLatest(int potId)
    {
        var reading = await _sensorRepo.GetLatestByPotIdAsync(potId);
        if (reading is null) return NotFound();

        return Ok(new SensorReadingResponse
        {
            Time = reading.Time,
            PotId = reading.PotId,
            SoilMoisture = reading.SoilMoisture,
            Temperature = reading.Temperature,
            Humidity = reading.Humidity,
            Lux = reading.Lux
        });
    }

    [HttpGet("{potId}/history")]
    public async Task<IActionResult> GetHistory(int potId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var readings = await _sensorRepo.GetByPotIdAsync(potId, from, to);
        return Ok(readings.Select(r => new SensorReadingResponse
        {
            Time = r.Time,
            PotId = r.PotId,
            SoilMoisture = r.SoilMoisture,
            Temperature = r.Temperature,
            Humidity = r.Humidity,
            Lux = r.Lux
        }));
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] SensorReadingRequest request)
    {
        // 1. Sačuvaj očitavanje
        var reading = new SensorReading
        {
            Time = DateTime.UtcNow,
            PotId = request.PotId,
            SoilMoisture = request.SoilMoisture,
            Temperature = request.Temperature,
            Humidity = request.Humidity,
            Lux = request.Lux
        };
        await _sensorRepo.AddAsync(reading);

        // 2. Dohvati aktivnu biljku
        var plant = await _plantRepo.GetActiveByPotIdAsync(request.PotId);
        if (plant is null) return Ok(new { message = "Nema aktivne biljke u saksiji." });

        var pot =
    await _potRepo.GetByIdAsync(request.PotId);

        if (pot is null)
        {
            return NotFound(new
            {
                message = "Saksija nije pronađena."
            });
        }

        var weather =
            await _weatherService.GetWeatherAsync();
        var rainUntilNextWindowEnd =
    CalculateRainUntilNextWateringWindowEnd(
        weather,
        DateTime.Now);

        // 3. Provjeri bolest — uzmi zadnju detekciju
        var lastDetection = await _diseaseDetectionRepo.GetLatestByPlantIdAsync(plant.Id);
        Disease? activeDisease = null;
        if (lastDetection is not null && !lastDetection.IsHealthy && lastDetection.DiseaseId.HasValue)
            activeDisease = await _diseaseRepo.GetByIdAsync(lastDetection.DiseaseId.Value);



        var decision =
    _ruleEngine.Evaluate(
        new WateringRuleInput
        {
            PlantId = plant.Id,

            CurrentSoilMoisture =
                request.SoilMoisture,

            MinRecommendedSoilMoisture =
                plant.PlantType.MinSoilMoisture,

            MaxRecommendedSoilMoisture =
                plant.PlantType.MaxSoilMoisture,

            // Ako je weather dostupan,
            // koristimo vanjsku temperaturu.
            // Inače fallback na senzor.
            CurrentTemperature =
                weather.IsAvailable
                    ? weather.CurrentTemp
                    : request.Temperature,

            MaxTemperatureNext24h =
                weather.IsAvailable
                    ? weather.MaxTempNext24h
                    : request.Temperature,

            CurrentLux =
                request.Lux,

            IsRainExposed =
                pot.IsRainExposed,

            WeatherAvailable =
                weather.IsAvailable,

            RainExpectedIn24h =
                weather.RainExpectedIn24h,

            RainAmountNext24hMm =
                weather.RainAmountNext24hMm,

            RainExpectedBeforeNextWindow =
    rainUntilNextWindowEnd.RainExpected,

            RainAmountBeforeNextWindowMm =
    rainUntilNextWindowEnd.RainAmountMm,

            RainIntensity =
                weather.RainIntensity,

            HeatRiskNext24h =
                weather.IsAvailable
                    ? weather.HeatRiskNext24h
                    : request.Temperature is >= 30,

            DiseaseWateringModifier =
    activeDisease?.WateringModifier
    ?? 1.00m,

            ActiveDiseaseName =
    activeDisease?.NameLocal
    ?? activeDisease?.Name,
            LocalNow =
                DateTime.Now
        });

        if (decision.IsAutomaticWateringAllowedNow)
        {
            await _wateringRepo.CreateAsync(new WateringEvent
            {
                PotId = request.PotId,
                TriggeredBy = "auto",
                DurationSec = 10,
                AmountMl = decision.RecommendedAmountMl,
                SoilMoistureBefore = request.SoilMoisture is null
                    ? null
                    : (int?)Math.Round(request.SoilMoisture.Value),
                SoilMoistureAfter = null,
                Skipped = false,
                SkipReason = null,
                IsForced = false,
                DecisionReason = decision.DecisionReason,
                WeatherSummary = BuildWeatherSummary(weather),
                CreatedAt = DateTime.UtcNow
            });

            await _recommendationService.CreateWateringRecommendationAsync(
                plant.Id,
                plant.PlantPot.UserId,
                decision.StatusMessage);
        }
        else
        {
            await _wateringRepo.CreateAsync(new WateringEvent
            {
                PotId = request.PotId,
                TriggeredBy = "auto",
                DurationSec = null,
                AmountMl = 0,
                SoilMoistureBefore = request.SoilMoisture is null
                    ? null
                    : (int?)Math.Round(request.SoilMoisture.Value),
                SoilMoistureAfter = null,
                Skipped = true,
                SkipReason =
    decision.WarningMessage
    ?? decision.WeatherImpactMessage
    ?? decision.StatusMessage,
                IsForced = false,
                DecisionReason = decision.DecisionReason,
                WeatherSummary = BuildWeatherSummary(weather),
                CreatedAt = DateTime.UtcNow
            });
        }

        return Ok(new
        {
            message = "Očitavanje sačuvano.",
            wateringDecision = decision
        });
    }
    private static string BuildWeatherSummary(
    WeatherResponse weather)
    {
        if (!weather.IsAvailable)
        {
            return
                "Vremenska prognoza nije dostupna.";
        }

        var rainText =
            weather.RainExpectedIn24h
                ? $"Očekivana kiša: " +
                  $"{weather.RainAmountNext24hMm:0.#} mm."
                : "Kiša nije očekivana.";

        var heatText =
            weather.HeatRiskNext24h
                ? $"Povećan toplotni rizik; " +
                  $"maksimalna temperatura naredna 24 h: " +
                  $"{weather.MaxTempNext24h:0.#} °C."
                : $"Maksimalna temperatura naredna 24 h: " +
                  $"{weather.MaxTempNext24h:0.#} °C.";

        return
            $"{weather.Description}. " +
            $"Trenutno: {weather.CurrentTemp:0.#} °C. " +
            $"{rainText} {heatText}";
    }
    private static (
    
    bool RainExpected,
    decimal RainAmountMm)
    CalculateRainUntilNextWateringWindowEnd(
        WeatherResponse weather,
        DateTime localNow)
    {
        if (!weather.IsAvailable ||
            weather.ForecastItems.Count == 0)
        {
            return (false, 0m);
        }

        var decisionHorizon =
            GetNextWateringDecisionHorizon(
                localNow);

        var decisionHorizonUtc =
            decisionHorizon.ToUniversalTime();

        var rainAmount =
        weather.ForecastItems
            .Where(f =>
                f.ForecastTimeUtc > DateTime.UtcNow &&
                f.ForecastTimeUtc <= decisionHorizonUtc)
            .Sum(f => f.RainMm);

    rainAmount =
        Math.Round(
            rainAmount,
            1);

    return (
        rainAmount > 0,
        rainAmount);
}

private static DateTime GetNextWateringDecisionHorizon(
    DateTime localNow)
    {
        var time =
            localNow.TimeOfDay;

        var morningStart =
            new TimeSpan(5, 0, 0);

        var morningEnd =
            new TimeSpan(8, 0, 0);

        var eveningStart =
            new TimeSpan(19, 0, 0);

        var eveningEnd =
            new TimeSpan(22, 0, 0);

        // Prije jutarnjeg termina:
        // gledamo prognozu do 08:00.
        if (time < morningStart)
        {
            return localNow.Date
            .AddHours(8);
    }

    // Tokom jutarnjeg termina:
    // gledamo do njegovog kraja.
    if (time <= morningEnd)
    {
        return localNow.Date
            .AddHours(8);
    }

    // Između jutarnjeg i večernjeg termina:
    // gledamo do 22:00.
    if (time < eveningStart)
{
    return localNow.Date
        .AddHours(22);
}

// Tokom večernjeg termina:
// gledamo do njegovog kraja.
if (time <= eveningEnd)
{
    return localNow.Date
        .AddHours(22);
}

// Poslije večernjeg termina:
// gledamo do kraja sutrašnjeg jutarnjeg termina.
return localNow.Date
    .AddDays(1)
    .AddHours(8);
}
}