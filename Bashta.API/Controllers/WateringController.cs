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
public class WateringController : ControllerBase
{
    private const int MaxManualWateringsPer24Hours = 2;
    private const int DefaultDurationSec = 10;
    private const int PendingCommandExpiryMinutes = 2;

    private readonly IWateringEventRepository _wateringRepo;
    private readonly ISensorReadingRepository _sensorRepo;
    private readonly IPlantRepository _plantRepo;
    private readonly IPlantPotRepository _potRepo;
    private readonly IDiseaseDetectionRepository _diseaseDetectionRepo;
    private readonly IDiseaseRepository _diseaseRepo;
    private readonly INotificationRepository _notificationRepo;
    private readonly WeatherService _weatherService;
    private readonly WateringRuleEngine _wateringRuleEngine;

    public WateringController(
        IWateringEventRepository wateringRepo,
        ISensorReadingRepository sensorRepo,
        IPlantRepository plantRepo,
        IPlantPotRepository potRepo,
        IDiseaseDetectionRepository diseaseDetectionRepo,
        IDiseaseRepository diseaseRepo,
        INotificationRepository notificationRepo,
        WeatherService weatherService,
        WateringRuleEngine wateringRuleEngine)
    {
        _wateringRepo = wateringRepo;
        _sensorRepo = sensorRepo;
        _plantRepo = plantRepo;
        _potRepo = potRepo;
        _diseaseDetectionRepo = diseaseDetectionRepo;
        _diseaseRepo = diseaseRepo;
        _notificationRepo = notificationRepo;
        _weatherService = weatherService;
        _wateringRuleEngine = wateringRuleEngine;
    }

    [HttpGet("{potId}")]
    public async Task<IActionResult> GetHistory(int potId, [FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 50);

        var pot = await _potRepo.GetByIdAsync(potId);
        if (pot is null)
            return NotFound(new { message = "Saksija nije pronađena." });

        if (pot.UserId != GetCurrentUserId())
            return Forbid();

        var events = await _wateringRepo.GetByPotIdAsync(potId, limit);
        return Ok(events.Select(MapToResponse));
    }

    [HttpGet("{potId}/status")]
    public async Task<IActionResult> GetStatus(int potId, [FromQuery] int limit = 5)
    {
        limit = Math.Clamp(limit, 1, 5);

        var pot = await _potRepo.GetByIdAsync(potId);
        if (pot is null)
            return NotFound(new { message = "Saksija nije pronađena." });

        if (pot.UserId != GetCurrentUserId())
            return Forbid();

        var activePlant = await _plantRepo.GetActiveByPotIdAsync(potId);
        Disease? activeDisease = null;

        if (activePlant is not null)
        {
            var lastDetection =
                await _diseaseDetectionRepo.GetLatestByPlantIdAsync(activePlant.Id);

            if (lastDetection is not null &&
                !lastDetection.IsHealthy &&
                lastDetection.DiseaseId.HasValue)
            {
                activeDisease =
                    await _diseaseRepo.GetByIdAsync(lastDetection.DiseaseId.Value);
            }
        }

        var latestReading = await _sensorRepo.GetLatestByPotIdAsync(potId);
        var latestWatering = await _wateringRepo.GetLatestByPotIdAsync(potId);
        var manualWateringCountLast24h =
            await _wateringRepo.CountManualNonSkippedByPotIdSinceAsync(
                potId,
                DateTime.UtcNow.AddHours(-24));

        var recentEvents =
            await _wateringRepo.GetRecentCompletedByPotIdAsync(potId, limit);

        var weather = await _weatherService.GetWeatherAsync();
        var rainUntilNextWindowEnd =
            CalculateRainUntilNextWateringWindowEnd(weather, DateTime.Now);

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
            WeatherAvailable = weather.IsAvailable,
            RainExpectedIn24h = weather.RainExpectedIn24h,
            RainAmountNext24hMm = weather.RainAmountNext24hMm,
            RainExpectedBeforeNextWindow = rainUntilNextWindowEnd.RainExpected,
            RainAmountBeforeNextWindowMm = rainUntilNextWindowEnd.RainAmountMm,
            RainIntensity = weather.RainIntensity,
            HeatRiskNext24h = weather.HeatRiskNext24h,
            DiseaseWateringModifier = activeDisease?.WateringModifier ?? 1.00m,
            ActiveDiseaseName = activeDisease?.NameLocal ?? activeDisease?.Name,
            LocalNow = DateTime.Now
        });

        var remainingManualWateringsLast24h =
            Math.Max(
                0,
                MaxManualWateringsPer24Hours - manualWateringCountLast24h);

        var manualWateringAvailable =
            pot.IsActive &&
            activePlant is not null &&
            remainingManualWateringsLast24h > 0;

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

            WateringCountLast24h = manualWateringCountLast24h,
            MaxWateringCountLast24h = MaxManualWateringsPer24Hours,
            RemainingWateringsLast24h = remainingManualWateringsLast24h,

            RecommendedAmountMl = decision.RecommendedAmountMl,
            CanWater = manualWateringAvailable,
            IsWateringRecommended = pot.IsActive && decision.IsWateringRecommended,
            RequiresForce = manualWateringAvailable && decision.RequiresForce,
            IsAutomaticWateringAllowedNow =
                pot.IsActive && decision.IsAutomaticWateringAllowedNow,
            NextRecommendedWateringWindow = pot.IsActive
                ? decision.NextRecommendedWateringWindow
                : "Nije dostupno dok je saksija neaktivna.",
            StatusMessage = pot.IsActive
                ? decision.StatusMessage
                : "Saksija je trenutno neaktivna.",
            WarningMessage = !pot.IsActive
                ? "Podaci i historija ostaju dostupni, ali su manuelno i automatsko zalijevanje onemogućeni."
                : remainingManualWateringsLast24h <= 0
                    ? "Dostignut je maksimalan broj od dva manuelna zalijevanja u posljednja 24 sata."
                    : decision.WarningMessage,
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

        if (pot.UserId != GetCurrentUserId())
            return Forbid();

        if (!pot.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Saksija je trenutno neaktivna. Aktivirajte je prije zalijevanja."
            });
        }

        var activePlant =
            await _plantRepo.GetActiveByPotIdAsync(request.PotId);

        if (activePlant is null)
        {
            return BadRequest(new
            {
                message = "Saksija nema aktivnu biljku."
            });
        }

        var manualWateringCountLast24h =
            await _wateringRepo.CountManualNonSkippedByPotIdSinceAsync(
                request.PotId,
                DateTime.UtcNow.AddHours(-24));

        if (manualWateringCountLast24h >= MaxManualWateringsPer24Hours)
        {
            return BadRequest(new
            {
                message =
                    "Dostignut je maksimalan broj od dva manuelna zalijevanja u posljednja 24 sata.",
                manualWateringCountLast24h,
                maxManualWateringsPer24h = MaxManualWateringsPer24Hours,
                remainingManualWateringsLast24h = 0
            });
        }

        var existingPending = await _wateringRepo.GetPendingManualByPotIdAsync(request.PotId);

        if (existingPending is not null)
        {
            var age = DateTime.UtcNow - existingPending.CreatedAt;

            if (age.TotalMinutes < PendingCommandExpiryMinutes)
                return Conflict(new { message = "Već postoji manuelna naredba koja čeka izvršenje na uređaju." });

            existingPending.DurationSec = null;
            existingPending.AmountMl = 0;
            existingPending.Skipped = true;
            existingPending.SkipReason = "Naredba nije preuzeta od IoT uređaja u predviđenom roku.";
            existingPending.DecisionReason = AppendReason(existingPending.DecisionReason, "commandStatus=expired");

            await _wateringRepo.UpdateAsync(existingPending);
        }

        Disease? activeDisease = null;
        var lastDetection =
            await _diseaseDetectionRepo.GetLatestByPlantIdAsync(activePlant.Id);

        if (lastDetection is not null &&
            !lastDetection.IsHealthy &&
            lastDetection.DiseaseId.HasValue)
        {
            activeDisease =
                await _diseaseRepo.GetByIdAsync(lastDetection.DiseaseId.Value);
        }

        var latestReading =
            await _sensorRepo.GetLatestByPotIdAsync(request.PotId);

        var weather = await _weatherService.GetWeatherAsync();
        var rainUntilNextWindowEnd =
            CalculateRainUntilNextWateringWindowEnd(weather, DateTime.Now);

        var decision = _wateringRuleEngine.Evaluate(new WateringRuleInput
        {
            PlantId = activePlant.Id,
            CurrentSoilMoisture = latestReading?.SoilMoisture,
            MinRecommendedSoilMoisture = activePlant.PlantType.MinSoilMoisture,
            MaxRecommendedSoilMoisture = activePlant.PlantType.MaxSoilMoisture,
            CurrentTemperature = weather.IsAvailable
                ? weather.CurrentTemp
                : latestReading?.Temperature,
            MaxTemperatureNext24h = weather.IsAvailable
                ? weather.MaxTempNext24h
                : latestReading?.Temperature,
            CurrentLux = latestReading?.Lux,
            IsRainExposed = pot.IsRainExposed,
            WeatherAvailable = weather.IsAvailable,
            RainExpectedIn24h = weather.RainExpectedIn24h,
            RainAmountNext24hMm = weather.RainAmountNext24hMm,
            RainExpectedBeforeNextWindow = rainUntilNextWindowEnd.RainExpected,
            RainAmountBeforeNextWindowMm = rainUntilNextWindowEnd.RainAmountMm,
            RainIntensity = weather.RainIntensity,
            HeatRiskNext24h = weather.HeatRiskNext24h,
            DiseaseWateringModifier = activeDisease?.WateringModifier ?? 1.00m,
            ActiveDiseaseName = activeDisease?.NameLocal ?? activeDisease?.Name,
            LocalNow = DateTime.Now
        });

        if (!decision.CanWater)
            return BadRequest(new { message = decision.StatusMessage, decision.DecisionReason });

        var forceRequired = decision.RequiresForce || !decision.IsWateringRecommended;

        if (forceRequired && !request.Force)
        {
            return BadRequest(new
            {
                message = "Zalijevanje trenutno nije preporučeno. Potrebna je potvrda korisnika.",
                requiresForce = true,
                statusMessage = decision.StatusMessage,
                warningMessage = decision.WarningMessage,
                weatherImpactMessage = decision.WeatherImpactMessage,
                diseaseImpactMessage = decision.DiseaseImpactMessage,
                recommendedAmountMl = decision.RecommendedAmountMl,
                decision.DecisionReason
            });
        }

        if (request.AmountMl is null)
            return BadRequest(new { message = "Unesite količinu manuelnog zalijevanja između 50 i 500 ml." });

        if (request.AmountMl is < 50 or > 500)
            return BadRequest(new { message = "Količina manuelnog zalijevanja mora biti između 50 i 500 ml." });

        var amountMl = request.AmountMl.Value;

        var wateringEvent = new WateringEvent
        {
            PotId = request.PotId,
            TriggeredBy = "manual",
            DurationSec = 0,
            AmountMl = amountMl,
            SoilMoistureBefore = latestReading?.SoilMoisture is null
                ? null
                : (int?)Math.Round(latestReading.SoilMoisture.Value),
            SoilMoistureAfter = null,
            Skipped = false,
            SkipReason = null,
            IsForced = forceRequired,
            DecisionReason = AppendReason(
                decision.DecisionReason,
                $"manualRequested=True; recommendedAmountMl={decision.RecommendedAmountMl}; selectedAmountMl={amountMl}; commandStatus=pending; requestedDurationSec={DefaultDurationSec}"),
            WeatherSummary = BuildWeatherSummary(weather),
            CreatedAt = DateTime.UtcNow
        };

        var created =
            await _wateringRepo.CreateAsync(wateringEvent);

        return Ok(MapToResponse(created));
    }
    [HttpGet("device/{potId}/config")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDeviceConfig(int potId)
    {
        var pot = await _potRepo.GetByIdAsync(potId);

        if (pot is null || !pot.IsActive)
            return NoContent();

        var intervalMinutes =
            pot.SensorReadingIntervalMinutes is 15 or 30 or 60
                ? pot.SensorReadingIntervalMinutes
                : 60;

        return Ok(new
        {
            potId = pot.Id,
            sensorReadingIntervalMinutes = intervalMinutes
        });
    }
    [HttpGet("device/{potId}/pending")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPendingDeviceCommand(int potId)
    {
        var pot = await _potRepo.GetByIdAsync(potId);
        if (pot is null || !pot.IsActive)
            return NoContent();

        var pending =
            await _wateringRepo.GetPendingManualByPotIdAsync(potId);

        if (pending is null)
            return NoContent();

        if ((DateTime.UtcNow - pending.CreatedAt).TotalMinutes >=
            PendingCommandExpiryMinutes)
        {
            pending.DurationSec = null;
            pending.AmountMl = 0;
            pending.Skipped = true;
            pending.SkipReason =
                "Naredba nije preuzeta od IoT uređaja u predviđenom roku.";
            pending.DecisionReason =
                AppendReason(pending.DecisionReason, "commandStatus=expired");

            await _wateringRepo.UpdateAsync(pending);
            return NoContent();
        }

        return Ok(new DeviceWateringCommandResponse
        {
            Id = pending.Id,
            PotId = pending.PotId,
            AmountMl = pending.AmountMl ?? 0,
            DurationSec = DefaultDurationSec,
            TriggeredBy = pending.TriggeredBy
        });
    }
    [HttpPost("device/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmDeviceExecution(
        [FromBody] WateringExecutionConfirmationRequest request)
    {
        if (request.EventId <= 0)
            return BadRequest(new { message = "EventId je obavezan." });

        var wateringEvent =
            await _wateringRepo.GetByIdAsync(request.EventId);

        if (wateringEvent is null)
            return NotFound(new { message = "Naredba za zalijevanje nije pronađena." });

        if (wateringEvent.TriggeredBy != "manual" ||
            wateringEvent.Skipped ||
            wateringEvent.DurationSec != 0)
        {
            return BadRequest(new
            {
                message = "Naredba više nije u pending stanju."
            });
        }

        var pot =
            await _potRepo.GetByIdAsync(wateringEvent.PotId);

        if (request.Success)
        {
            wateringEvent.DurationSec = DefaultDurationSec;
            wateringEvent.Skipped = false;
            wateringEvent.SkipReason = null;
            wateringEvent.DecisionReason =
                AppendReason(
                    wateringEvent.DecisionReason,
                    $"commandStatus=completed; waterLevel={request.WaterLevel:0.#}%");
        }
        else
        {
            var reason = string.IsNullOrWhiteSpace(request.FailureReason)
                ? "IoT uređaj nije mogao izvršiti zalijevanje."
                : request.FailureReason.Trim();

            wateringEvent.DurationSec = null;
            wateringEvent.AmountMl = 0;
            wateringEvent.Skipped = true;
            wateringEvent.SkipReason = reason;
            wateringEvent.DecisionReason =
                AppendReason(
                    wateringEvent.DecisionReason,
                    $"commandStatus=failed; waterLevel={request.WaterLevel:0.#}%");

            if (pot is not null)
            {
                await _notificationRepo.CreateAsync(
                    new Notification
                    {
                        UserId = pot.UserId,
                        Title = "Zalijevanje nije izvršeno",
                        Body =
                            $"Manuelno zalijevanje saksije \"{pot.Name}\" nije izvršeno. {reason}",
                        Type = "alert",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
            }
        }

        var updated =
            await _wateringRepo.UpdateAsync(wateringEvent);

        return Ok(MapToResponse(updated));
    }

    private static string AppendReason(string? existing, string value) =>
        string.IsNullOrWhiteSpace(existing) ? value : $"{existing}; {value}";

    private static (
        bool RainExpected,
        decimal RainAmountMm)
        CalculateRainUntilNextWateringWindowEnd(
            WeatherResponse weather,
            DateTime localNow)
    {
        if (!weather.IsAvailable || weather.ForecastItems.Count == 0)
            return (false, 0m);

        var decisionHorizon = GetNextWateringDecisionHorizon(localNow);
        var decisionHorizonUtc = decisionHorizon.ToUniversalTime();

        var rainAmount = weather.ForecastItems
            .Where(f =>
                f.ForecastTimeUtc > DateTime.UtcNow &&
                f.ForecastTimeUtc <= decisionHorizonUtc)
            .Sum(f => f.RainMm);

        rainAmount = Math.Round(rainAmount, 1);
        return (rainAmount > 0, rainAmount);
    }

    private static DateTime GetNextWateringDecisionHorizon(DateTime localNow)
    {
        var time = localNow.TimeOfDay;
        var morningStart = new TimeSpan(5, 0, 0);
        var morningEnd = new TimeSpan(8, 0, 0);
        var eveningStart = new TimeSpan(19, 0, 0);
        var eveningEnd = new TimeSpan(22, 0, 0);

        if (time < morningStart)
            return localNow.Date.AddHours(8);

        if (time <= morningEnd)
            return localNow.Date.AddHours(8);

        if (time < eveningStart)
            return localNow.Date.AddHours(22);

        if (time <= eveningEnd)
            return localNow.Date.AddHours(22);

        return localNow.Date.AddDays(1).AddHours(8);
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

        return
            $"{weather.Description}. Trenutno: {weather.CurrentTemp:0.#} °C. " +
            $"{rainText} {heatText}";
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

    private int GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(value, out var userId))
            throw new UnauthorizedAccessException();

        return userId;
    }
}
