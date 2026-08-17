using Bashta.Core.DTOs;
using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Core.Services;
using Bashta.Infrastructure.External;
using Microsoft.AspNetCore.Mvc;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WateringController : ControllerBase
{
    private const int MaxManualWateringsPer24Hours = 2;
    private const int DefaultDurationSec = 10;
    private const int DefaultManualFallbackAmountMl = 100;

    private readonly IWateringEventRepository _wateringRepo;
    private readonly ISensorReadingRepository _sensorRepo;
    private readonly IPlantRepository _plantRepo;
    private readonly IPlantPotRepository _potRepo;
    private readonly WeatherService _weatherService;
    private readonly WateringRuleEngine _wateringRuleEngine;
    private readonly IDiseaseDetectionRepository _diseaseDetectionRepo;
    private readonly IDiseaseRepository _diseaseRepo;

    public WateringController(
        IWateringEventRepository wateringRepo,
        ISensorReadingRepository sensorRepo,
        IPlantRepository plantRepo,
        IPlantPotRepository potRepo,
        IDiseaseDetectionRepository diseaseDetectionRepo,
        IDiseaseRepository diseaseRepo,
        WeatherService weatherService,
        WateringRuleEngine wateringRuleEngine)
        {
            _wateringRepo = wateringRepo;
            _sensorRepo = sensorRepo;
            _plantRepo = plantRepo;
            _potRepo = potRepo;
            _diseaseDetectionRepo = diseaseDetectionRepo;
            _diseaseRepo = diseaseRepo;
            _weatherService = weatherService;
            _wateringRuleEngine = wateringRuleEngine;
        }

    [HttpGet("{potId}")]
    public async Task<IActionResult> GetHistory(int potId, [FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 50);

        var events = await _wateringRepo.GetByPotIdAsync(potId, limit);

        return Ok(events.Select(MapToResponse));
    }

    [HttpGet("{potId}/status")]
    public async Task<IActionResult> GetStatus(int potId, [FromQuery] int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 20);

        var pot = await _potRepo.GetByIdAsync(potId);

        if (pot is null)
            return NotFound(new { message = "Saksija nije pronađena." });

        var activePlant = await _plantRepo.GetActiveByPotIdAsync(potId);
        Disease? activeDisease = null;

        if (activePlant is not null)
        {
            var lastDetection =
                await _diseaseDetectionRepo
                    .GetLatestByPlantIdAsync(activePlant.Id);

            if (lastDetection is not null &&
                !lastDetection.IsHealthy &&
                lastDetection.DiseaseId.HasValue)
            {
                activeDisease =
                    await _diseaseRepo.GetByIdAsync(
                        lastDetection.DiseaseId.Value);
            }
        }
        var latestReading = await _sensorRepo.GetLatestByPotIdAsync(potId);
        var latestWatering = await _wateringRepo.GetLatestByPotIdAsync(potId);

        var sinceUtc = DateTime.UtcNow.AddHours(-24);

        var manualWateringCountLast24h =
    await _wateringRepo.CountManualNonSkippedByPotIdSinceAsync(
        potId,
        sinceUtc);

        var recentEvents = await _wateringRepo.GetByPotIdAsync(potId, limit);

        var remaining =
     Math.Max(
         0,
         MaxManualWateringsPer24Hours -
         manualWateringCountLast24h);

        var weather = await _weatherService.GetWeatherAsync();
        var rainUntilNextWindowEnd =
    CalculateRainUntilNextWateringWindowEnd(
        weather,
        DateTime.Now);
        var rainBeforeNextWindow =
    CalculateRainBeforeNextWateringWindow(
        weather,
        DateTime.Now);

        var decision = _wateringRuleEngine.Evaluate(new WateringRuleInput
        {
            PlantId = activePlant?.Id,
            CurrentSoilMoisture = latestReading?.SoilMoisture,
            MinRecommendedSoilMoisture = activePlant?.PlantType.MinSoilMoisture,
            MaxRecommendedSoilMoisture = activePlant?.PlantType.MaxSoilMoisture,
            CurrentTemperature = weather.IsAvailable
                ? weather.CurrentTemp
                : latestReading?.Temperature,
            MaxTemperatureNext24h = weather.IsAvailable
                ? weather.MaxTempNext24h
                : latestReading?.Temperature,
            CurrentLux = latestReading?.Lux,
            IsRainExposed = pot.IsRainExposed,
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
    weather.HeatRiskNext24h,

            DiseaseWateringModifier =
    activeDisease?.WateringModifier
    ?? 1.00m,

            ActiveDiseaseName =
    activeDisease?.NameLocal
    ?? activeDisease?.Name,

            LocalNow =
    DateTime.Now
        });

        var response = new WateringStatusResponse
        {
            PotId = potId,
            PlantId = activePlant?.Id,
            PlantName = activePlant?.Nickname
                ?? activePlant?.PlantType.NameLocal
                ?? activePlant?.PlantType.Name,

            CurrentSoilMoisture = latestReading?.SoilMoisture,
            LastSensorReadingAt = latestReading?.Time,
            NextExpectedSensorReadingAt = latestReading is null
                ? null
                : latestReading.Time.AddMinutes(pot.SensorReadingIntervalMinutes),

            MinRecommendedSoilMoisture = activePlant?.PlantType.MinSoilMoisture,
            MaxRecommendedSoilMoisture = activePlant?.PlantType.MaxSoilMoisture,

            LastWateredAt = latestWatering?.CreatedAt,

            WateringCountLast24h =
    manualWateringCountLast24h,

            MaxWateringCountLast24h =
    MaxManualWateringsPer24Hours,

            RemainingWateringsLast24h =
    remaining,

            RecommendedAmountMl = decision.RecommendedAmountMl,
            CanWater = decision.CanWater,
            IsWateringRecommended = decision.IsWateringRecommended,
            RequiresForce = decision.RequiresForce,
            IsAutomaticWateringAllowedNow = decision.IsAutomaticWateringAllowedNow,
            NextRecommendedWateringWindow = decision.NextRecommendedWateringWindow,
            StatusMessage = decision.StatusMessage,
            WarningMessage = decision.WarningMessage,

            IsRainExposed = pot.IsRainExposed,
            WeatherAvailable = weather.IsAvailable,
            WeatherSummary = BuildWeatherSummary(weather),
            RainExpectedIn24h = weather.RainExpectedIn24h,
            RainAmountNext24hMm = weather.RainAmountNext24hMm,
            RainIntensity = weather.RainIntensity,
            CurrentOutdoorTemperature = weather.IsAvailable
                ? weather.CurrentTemp
                : null,
            MaxTemperatureNext24h = weather.IsAvailable
                ? weather.MaxTempNext24h
                : null,
            HeatRiskNext24h = weather.HeatRiskNext24h,
            WeatherImpactMessage = decision.WeatherImpactMessage,

            RecentEvents = recentEvents
                .Select(MapToResponse)
                .ToList()
        };

        return Ok(response);
    }

    [HttpPost("manual")]
    public async Task<IActionResult> ManualWater([FromBody] ManualWateringRequest request)
    {
        if (request.PotId <= 0)
            return BadRequest(new { message = "PotId je obavezan." });

        var pot = await _potRepo.GetByIdAsync(request.PotId);

        if (pot is null)
            return NotFound(new { message = "Saksija nije pronađena." });

        var activePlant = await _plantRepo.GetActiveByPotIdAsync(request.PotId);

        if (activePlant is null)
            return BadRequest(new { message = "Saksija nema aktivnu biljku." });
        Disease? activeDisease = null;

        var lastDetection =
            await _diseaseDetectionRepo
                .GetLatestByPlantIdAsync(activePlant.Id);

        if (lastDetection is not null &&
            !lastDetection.IsHealthy &&
            lastDetection.DiseaseId.HasValue)
        {
            activeDisease =
                await _diseaseRepo.GetByIdAsync(
                    lastDetection.DiseaseId.Value);
        }

        var sinceUtc = DateTime.UtcNow.AddHours(-24);

        var manualWateringCountLast24h =
    await _wateringRepo.CountManualNonSkippedByPotIdSinceAsync(
        request.PotId,
        sinceUtc);
        if (manualWateringCountLast24h >=
    MaxManualWateringsPer24Hours)
        {
            return BadRequest(new
            {
                message =
                    "Dostignut je maksimalan broj od dva manuelna zalijevanja u posljednja 24 sata.",

                manualWateringCountLast24h,

                maxManualWateringsPer24h =
                    MaxManualWateringsPer24Hours,

                remainingManualWateringsLast24h = 0
            });
        }

        var latestReading = await _sensorRepo.GetLatestByPotIdAsync(request.PotId);
        var weather = await _weatherService.GetWeatherAsync();
        var rainUntilNextWindowEnd =
    CalculateRainUntilNextWateringWindowEnd(
        weather,
        DateTime.Now);
        var rainBeforeNextWindow =
    CalculateRainBeforeNextWateringWindow(
        weather,
        DateTime.Now);

        var decision =
    _wateringRuleEngine.Evaluate(
        new WateringRuleInput
        {
            PlantId =
                activePlant.Id,

            CurrentSoilMoisture =
                latestReading?.SoilMoisture,

            MinRecommendedSoilMoisture =
                activePlant.PlantType.MinSoilMoisture,

            MaxRecommendedSoilMoisture =
                activePlant.PlantType.MaxSoilMoisture,

            CurrentTemperature =
                weather.IsAvailable
                    ? weather.CurrentTemp
                    : latestReading?.Temperature,

            MaxTemperatureNext24h =
                weather.IsAvailable
                    ? weather.MaxTempNext24h
                    : latestReading?.Temperature,

            CurrentLux =
                latestReading?.Lux,

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
    weather.HeatRiskNext24h,

            DiseaseWateringModifier =
    activeDisease?.WateringModifier
    ?? 1.00m,

            ActiveDiseaseName =
    activeDisease?.NameLocal
    ?? activeDisease?.Name,

            LocalNow =
    DateTime.Now
        });

        if (!decision.CanWater)
        {
            return BadRequest(new
            {
                message =
                    decision.StatusMessage,

                decision.DecisionReason
            });
        }

        if (decision.RequiresForce &&
            !request.Force)
        {
            return BadRequest(new
            {
                message =
                    "Zalijevanje trenutno nije preporučeno. " +
                    "Korisnik može nastaviti samo u force modu.",

                requiresForce = true,

                statusMessage =
                    decision.StatusMessage,

                warningMessage =
                    decision.WarningMessage,

                weatherImpactMessage =
                    decision.WeatherImpactMessage,

                diseaseImpactMessage =
                    decision.DiseaseImpactMessage,

                recommendedAmountMl =
                    decision.RecommendedAmountMl,

                decision.DecisionReason
            });
        }

        /*
         * Manuelni unos količine:
         * dozvoljen raspon 50–500 ml.
         */
        if (request.AmountMl is < 50 or > 500)
        {
            return BadRequest(new
            {
                message =
                    "Količina manuelnog zalijevanja mora biti između 50 i 500 ml."
            });
        }

        /*
         * Ako je korisnik unio količinu,
         * koristi se njegova vrijednost.
         *
         * Inače se koristi količina koju
         * preporučuje WateringRuleEngine.
         */
        var amountMl =
            request.AmountMl
            ?? decision.RecommendedAmountMl;

        /*
         * Fallback u slučaju da engine vrati 0,
         * a korisnik je eksplicitno odobrio
         * manuelno zalijevanje.
         */
        if (amountMl <= 0)
        {
            amountMl =
                DefaultManualFallbackAmountMl;
        }

        var durationSec =
            request.DurationSec > 0
                ? request.DurationSec
                : DefaultDurationSec;

        var wateringEvent =
            new WateringEvent
            {
                PotId =
                    request.PotId,

                TriggeredBy =
                    "manual",

                DurationSec =
                    durationSec,

                AmountMl =
                    amountMl,

                SoilMoistureBefore =
                    latestReading?.SoilMoisture is null
                        ? null
                        : (int?)Math.Round(
                            latestReading.SoilMoisture.Value),

                SoilMoistureAfter =
                    null,

                Skipped =
                    false,

                SkipReason =
                    null,

                IsForced =
                    request.Force,

                DecisionReason =
                    decision.DecisionReason,

                WeatherSummary =
                    BuildWeatherSummary(weather),

                CreatedAt =
                    DateTime.UtcNow
            };

        var created =
            await _wateringRepo.CreateAsync(
                wateringEvent);

        return Ok(
            MapToResponse(created));
    }
    private static (
    bool RainExpected,
    decimal RainAmountMm)
    CalculateRainBeforeNextWateringWindow(
        WeatherResponse weather,
        DateTime localNow)
    {
        if (!weather.IsAvailable ||
            weather.ForecastItems.Count == 0)
        {
            return (false, 0m);
        }

        var nextWindow =
            GetNextWateringWindowStart(
                localNow);

        var nextWindowUtc =
            nextWindow.ToUniversalTime();

        var rainAmount =
            weather.ForecastItems
                .Where(f =>
                    f.ForecastTimeUtc >
                        DateTime.UtcNow &&
                    f.ForecastTimeUtc <=
                        nextWindowUtc)
                .Sum(f => f.RainMm);

        rainAmount =
            Math.Round(
                rainAmount,
                1);

        return (
            rainAmount > 0,
            rainAmount);
    }
    private static DateTime GetNextWateringWindowStart(
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
                .AddHours(5);
        }

        if (time <= morningEnd)
        {
            return localNow.Date
                .AddHours(19);
        }

        if (time < eveningStart)
        {
            return localNow.Date
                .AddHours(19);
        }

        if (time <= eveningEnd)
        {
            return localNow.Date
                .AddDays(1)
                .AddHours(5);
        }

        return localNow.Date
            .AddDays(1)
            .AddHours(5);
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
    private static string BuildWeatherSummary(WeatherResponse weather)
    {
        if (!weather.IsAvailable)
            return "Vremenska prognoza nije dostupna.";

        var rainText = weather.RainExpectedIn24h
            ? $"Očekivana kiša: {weather.RainAmountNext24hMm:0.#} mm u naredna 24h."
            : "Kiša nije očekivana u naredna 24h.";

        var heatText = weather.HeatRiskNext24h
            ? $"Moguć toplotni stres. Maksimalna prognozirana temperatura: {weather.MaxTempNext24h:0.#} °C."
            : $"Maksimalna prognozirana temperatura: {weather.MaxTempNext24h:0.#} °C.";

        return $"{weather.Description}. Trenutno: {weather.CurrentTemp:0.#} °C. {rainText} {heatText}";
    }

    private static WateringEventResponse MapToResponse(WateringEvent wateringEvent)
    {
        return new WateringEventResponse
        {
            Id = wateringEvent.Id,
            PotId = wateringEvent.PotId,
            TriggeredBy = wateringEvent.TriggeredBy,
            DurationSec = wateringEvent.DurationSec,
            AmountMl = wateringEvent.AmountMl,
            SoilMoistureBefore = wateringEvent.SoilMoistureBefore,
            SoilMoistureAfter = wateringEvent.SoilMoistureAfter,
            Skipped = wateringEvent.Skipped,
            SkipReason = wateringEvent.SkipReason,
            IsForced = wateringEvent.IsForced,
            DecisionReason = wateringEvent.DecisionReason,
            WeatherSummary = wateringEvent.WeatherSummary,
            CreatedAt = wateringEvent.CreatedAt
        };
    }
}