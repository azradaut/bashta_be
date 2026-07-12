using Bashta.Core.DTOs;
using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WateringController : ControllerBase
{
    private const int MaxWateringsPer24Hours = 2;
    private const int DefaultAmountMl = 200;
    private const int DefaultDurationSec = 10;
    private const int HighSunLuxThreshold = 10000;

    private readonly IWateringEventRepository _wateringRepo;
    private readonly ISensorReadingRepository _sensorRepo;
    private readonly IPlantRepository _plantRepo;

    public WateringController(
        IWateringEventRepository wateringRepo,
        ISensorReadingRepository sensorRepo,
        IPlantRepository plantRepo)
    {
        _wateringRepo = wateringRepo;
        _sensorRepo = sensorRepo;
        _plantRepo = plantRepo;
    }

    [HttpGet("{potId}")]
    public async Task<IActionResult> GetHistory(int potId, [FromQuery] int limit = 20)
    {
        var events = await _wateringRepo.GetByPotIdAsync(potId, limit);

        return Ok(events.Select(MapToResponse));
    }

    [HttpGet("{potId}/status")]
    public async Task<IActionResult> GetStatus(int potId, [FromQuery] int limit = 10)
    {
        var activePlant = await _plantRepo.GetActiveByPotIdAsync(potId);
        var latestReading = await _sensorRepo.GetLatestByPotIdAsync(potId);
        var latestWatering = await _wateringRepo.GetLatestByPotIdAsync(potId);

        var sinceUtc = DateTime.UtcNow.AddHours(-24);
        var wateringCountLast24h = await _wateringRepo.CountNonSkippedByPotIdSinceAsync(potId, sinceUtc);

        var recentEvents = await _wateringRepo.GetByPotIdAsync(potId, limit);

        var remaining = Math.Max(0, MaxWateringsPer24Hours - wateringCountLast24h);

        var response = new WateringStatusResponse
        {
            PotId = potId,
            PlantId = activePlant?.Id,
            PlantName = activePlant?.Nickname
                ?? activePlant?.PlantType.NameLocal
                ?? activePlant?.PlantType.Name,

            CurrentSoilMoisture = latestReading?.SoilMoisture,
            LastSensorReadingAt = latestReading?.Time,

            MinRecommendedSoilMoisture = activePlant?.PlantType.MinSoilMoisture,
            MaxRecommendedSoilMoisture = activePlant?.PlantType.MaxSoilMoisture,

            LastWateredAt = latestWatering?.CreatedAt,

            WateringCountLast24h = wateringCountLast24h,
            MaxWateringCountLast24h = MaxWateringsPer24Hours,
            RemainingWateringsLast24h = remaining,

            RecommendedAmountMl = DefaultAmountMl,

            RecentEvents = recentEvents
                .Select(MapToResponse)
                .ToList()
        };

        ApplyWateringDecision(response, latestReading?.Lux);

        return Ok(response);
    }

    [HttpPost("manual")]
    public async Task<IActionResult> ManualWater([FromBody] ManualWateringRequest request)
    {
        if (request.PotId <= 0)
            return BadRequest(new { message = "PotId je obavezan." });

        var activePlant = await _plantRepo.GetActiveByPotIdAsync(request.PotId);

        if (activePlant is null)
            return BadRequest(new { message = "Saksija nema aktivnu biljku." });

        var sinceUtc = DateTime.UtcNow.AddHours(-24);
        var wateringCountLast24h = await _wateringRepo.CountNonSkippedByPotIdSinceAsync(
            request.PotId,
            sinceUtc);

        if (wateringCountLast24h >= MaxWateringsPer24Hours)
        {
            return BadRequest(new
            {
                message = "Dostignut je limit od 2 zalijevanja u posljednja 24 sata."
            });
        }

        var latestReading = await _sensorRepo.GetLatestByPotIdAsync(request.PotId);

        var amountMl = request.AmountMl.GetValueOrDefault(DefaultAmountMl);
        var durationSec = request.DurationSec > 0
            ? request.DurationSec
            : DefaultDurationSec;

        var wateringEvent = new WateringEvent
        {
            PotId = request.PotId,
            TriggeredBy = "manual",
            DurationSec = durationSec,
            AmountMl = amountMl,
            SoilMoistureBefore = latestReading?.SoilMoisture is null
                ? null
                : (int?)Math.Round(latestReading.SoilMoisture.Value),
            Skipped = false,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _wateringRepo.CreateAsync(wateringEvent);

        return Ok(MapToResponse(created));
    }

    private static void ApplyWateringDecision(
        WateringStatusResponse response,
        int? currentLux)
    {
        if (response.PlantId is null)
        {
            response.CanWater = false;
            response.StatusMessage = "Saksija nema aktivnu biljku.";
            return;
        }

        if (response.WateringCountLast24h >= response.MaxWateringCountLast24h)
        {
            response.CanWater = false;
            response.StatusMessage = "Zalijevanje nije dostupno jer je dostignut limit od 2 zalijevanja u posljednja 24 sata.";
            return;
        }

        if (response.CurrentSoilMoisture is null)
        {
            response.CanWater = true;
            response.StatusMessage = "Nema dostupnog očitanja vlažnosti tla. Zalijevanje je moguće, ali se preporučuje provjera senzora.";
            return;
        }

        if (response.MinRecommendedSoilMoisture is null ||
            response.MaxRecommendedSoilMoisture is null)
        {
            response.CanWater = true;
            response.StatusMessage = "Nema definisanog preporučenog opsega vlage za ovu biljku.";
            return;
        }

        if (response.CurrentSoilMoisture < response.MinRecommendedSoilMoisture)
        {
            response.CanWater = true;
            response.StatusMessage = "Vlažnost tla je ispod preporučenog opsega. Preporučuje se zalijevanje.";
        }
        else if (response.CurrentSoilMoisture > response.MaxRecommendedSoilMoisture)
        {
            response.CanWater = true;
            response.StatusMessage = "Vlažnost tla je iznad preporučenog opsega. Zalijevanje trenutno nije preporučeno.";
        }
        else
        {
            response.CanWater = true;
            response.StatusMessage = "Vlažnost tla je u preporučenom opsegu. Zalijevanje trenutno nije potrebno.";
        }

        var localHour = DateTime.Now.Hour;
        var isStrongSunPeriod = localHour is >= 10 and <= 17;
        var isHighLux = currentLux >= HighSunLuxThreshold;

        if (isStrongSunPeriod || isHighLux)
        {
            response.WarningMessage = "Preporučuje se zalijevanje ujutro ili navečer, posebno tokom jakog sunca.";
        }
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
            CreatedAt = wateringEvent.CreatedAt
        };
    }
}