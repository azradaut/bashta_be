using Bashta.Core.DTOs;
using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Core.Services;
using Bashta.Infrastructure.External;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SensorController : ControllerBase
{
    private const decimal LowWaterLevelThreshold = 15m;

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
        var pot = await _potRepo.GetByIdAsync(potId);

        if (pot is null)
            return NotFound();

        if (pot.UserId != GetCurrentUserId())
            return Forbid();

        var reading = await _sensorRepo.GetLatestByPotIdAsync(potId);

        if (reading is null)
            return NotFound();

        return Ok(new SensorReadingResponse
        {
            Time = reading.Time,
            PotId = reading.PotId,
            SoilMoisture = reading.SoilMoisture,
            Temperature = reading.Temperature,
            Humidity = reading.Humidity,
            Lux = reading.Lux,
            WaterLevel = reading.WaterLevel
        });
    }

    [HttpGet("{potId}/history")]
    public async Task<IActionResult> GetHistory(
        int potId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var pot = await _potRepo.GetByIdAsync(potId);

        if (pot is null)
            return NotFound();

        if (pot.UserId != GetCurrentUserId())
            return Forbid();

        var readings =
            await _sensorRepo.GetByPotIdAsync(
                potId,
                from,
                to);

        return Ok(readings.Select(r => new SensorReadingResponse
        {
            Time = r.Time,
            PotId = r.PotId,
            SoilMoisture = r.SoilMoisture,
            Temperature = r.Temperature,
            Humidity = r.Humidity,
            Lux = r.Lux,
            WaterLevel = r.WaterLevel
        }));
    }

    [HttpPost("ingest")]
    [AllowAnonymous]
    public async Task<IActionResult> Ingest(
        [FromBody] SensorReadingRequest request)
    {
        // 1. Provjeri da saksija postoji
        var pot =
            await _potRepo.GetByIdAsync(
                request.PotId);

        if (pot is null)
        {
            return NotFound(new
            {
                message = "Saksija nije pronađena."
            });
        }

        // 2. Prethodno očitanje uzimamo prije upisa novog.
        // Koristi se za detekciju prelaska nivoa vode
        // iz normalnog u nizak.
        var previousReading =
            await _sensorRepo.GetLatestByPotIdAsync(
                request.PotId);

        // 3. Sačuvaj novo senzorsko očitanje
        var reading = new SensorReading
        {
            Time = DateTime.UtcNow,
            PotId = request.PotId,
            SoilMoisture = request.SoilMoisture,
            Temperature = request.Temperature,
            Humidity = request.Humidity,
            Lux = request.Lux,
            WaterLevel = request.WaterLevel
        };

        await _sensorRepo.AddAsync(reading);

        // 4. Ako je saksija neaktivna, reading ostaje sačuvan,
        // ali se ne pokreću obavijesti ni watering logika.
        if (!pot.IsActive)
        {
            return Ok(new
            {
                message =
                    "Senzorsko očitavanje je sačuvano, " +
                    "ali je saksija neaktivna. " +
                    "Automatska procjena zalijevanja nije pokrenuta."
            });
        }

        // 5. Provjera nivoa vode
        var reservoirLow =
            request.WaterLevel.HasValue &&
            request.WaterLevel.Value <= LowWaterLevelThreshold;

        var wasPreviouslyLow =
            previousReading?.WaterLevel.HasValue == true &&
            previousReading.WaterLevel.Value <= LowWaterLevelThreshold;

        // Notification se kreira samo prilikom prelaska
        // iz normalnog nivoa u nizak nivo.
        if (reservoirLow && !wasPreviouslyLow)
        {
            await _notificationRepo.CreateAsync(
                new Notification
                {
                    UserId = pot.UserId,
                    Title = "Nizak nivo vode",
                    Body =
                        $"Nivo vode u rezervoaru saksije \"{pot.Name}\" " +
                        $"je {request.WaterLevel:0.#}%. " +
                        "Dopunite rezervoar prije narednog zalijevanja.",
                    Type = "alert",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
        }

        // 6. Dohvati aktivnu biljku
        var plant =
            await _plantRepo.GetActiveByPotIdAsync(
                request.PotId);

        if (plant is null)
        {
            return Ok(new
            {
                message =
                    "Očitavanje je sačuvano, ali nema aktivne biljke u saksiji."
            });
        }

        // 7. Vremenski podaci
        var weather =
            await _weatherService.GetWeatherAsync();

        var rainUntilNextWindowEnd =
            CalculateRainUntilNextWateringWindowEnd(
                weather,
                DateTime.Now);

        // 8. Posljednja detekcija bolesti
        var lastDetection =
            await _diseaseDetectionRepo
                .GetLatestByPlantIdAsync(
                    plant.Id);

        Disease? activeDisease = null;

        if (lastDetection is not null &&
            !lastDetection.IsHealthy &&
            lastDetection.DiseaseId.HasValue)
        {
            activeDisease =
                await _diseaseRepo.GetByIdAsync(
                    lastDetection.DiseaseId.Value);
        }

        // 9. WateringRuleEngine donosi odluku
        // na osnovu stanja biljke, senzora i vremena.
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

        // 10. Operativna zaštita rezervoara.
        //
        // Engine može zaključiti da biljci treba voda,
        // ali ako je rezervoar na 15% ili manje,
        // fizičko automatsko zalijevanje nije dozvoljeno.
        if (reservoirLow &&
            decision.IsWateringRecommended)
        {
            decision.CanWater = false;
            decision.IsAutomaticWateringAllowedNow = false;

            decision.WarningMessage =
                $"Automatsko zalijevanje nije izvršeno jer je " +
                $"nivo vode u rezervoaru {request.WaterLevel:0.#}%. " +
                $"Za zalijevanje je potreban nivo vode iznad " +
                $"{LowWaterLevelThreshold:0.#}%.";

            decision.DecisionReason +=
                $"; waterLevel={request.WaterLevel:0.#}%" +
                "; reservoirLow=True" +
                "; autoAllowedNow=False";
        }

        // 11. Evidencija watering odluke
        if (decision.IsAutomaticWateringAllowedNow)
        {
            await _wateringRepo.CreateAsync(
                new WateringEvent
                {
                    PotId = request.PotId,
                    TriggeredBy = "auto",
                    DurationSec = 10,
                    AmountMl =
                        decision.RecommendedAmountMl,

                    SoilMoistureBefore =
                        request.SoilMoisture is null
                            ? null
                            : (int?)Math.Round(
                                request.SoilMoisture.Value),

                    SoilMoistureAfter = null,
                    Skipped = false,
                    SkipReason = null,
                    IsForced = false,

                    DecisionReason =
                        decision.DecisionReason,

                    WeatherSummary =
                        BuildWeatherSummary(weather),

                    CreatedAt =
                        DateTime.UtcNow
                });

            await _recommendationService
                .CreateWateringRecommendationAsync(
                    plant.Id,
                    pot.UserId,
                    decision.StatusMessage);
        }
        else
        {
            await _wateringRepo.CreateAsync(
                new WateringEvent
                {
                    PotId = request.PotId,
                    TriggeredBy = "auto",
                    DurationSec = null,
                    AmountMl = 0,

                    SoilMoistureBefore =
                        request.SoilMoisture is null
                            ? null
                            : (int?)Math.Round(
                                request.SoilMoisture.Value),

                    SoilMoistureAfter = null,
                    Skipped = true,

                    SkipReason =
                        decision.WarningMessage
                        ?? decision.WeatherImpactMessage
                        ?? decision.StatusMessage,

                    IsForced = false,

                    DecisionReason =
                        decision.DecisionReason,

                    WeatherSummary =
                        BuildWeatherSummary(weather),

                    CreatedAt =
                        DateTime.UtcNow
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

    private int GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!int.TryParse(
                value,
                out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        return userId;
    }

    private static DateTime
        GetNextWateringDecisionHorizon(
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

        if (time < morningStart)
        {
            return localNow.Date
                .AddHours(8);
        }

        if (time <= morningEnd)
        {
            return localNow.Date
                .AddHours(8);
        }

        if (time < eveningStart)
        {
            return localNow.Date
                .AddHours(22);
        }

        if (time <= eveningEnd)
        {
            return localNow.Date
                .AddHours(22);
        }

        return localNow.Date
            .AddDays(1)
            .AddHours(8);
    }
}