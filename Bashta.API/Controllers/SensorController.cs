using Bashta.Core.DTOs;
using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Core.Services;
using Microsoft.AspNetCore.Mvc;

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

    public SensorController(
        ISensorReadingRepository sensorRepo,
        IPlantRepository plantRepo,
        IWateringEventRepository wateringRepo,
        IDiseaseDetectionRepository diseaseDetectionRepo,
        IDiseaseRepository diseaseRepo,
        IRecommendationRepository recommendationRepo,
        INotificationRepository notificationRepo,
        WateringRuleEngine ruleEngine,
        DLIService dliService,
        RecommendationService recommendationService)
    {
        _sensorRepo = sensorRepo;
        _plantRepo = plantRepo;
        _wateringRepo = wateringRepo;
        _diseaseDetectionRepo = diseaseDetectionRepo;
        _diseaseRepo = diseaseRepo;
        _recommendationRepo = recommendationRepo;
        _notificationRepo = notificationRepo;
        _ruleEngine = ruleEngine;
        _dliService = dliService;
        _recommendationService = recommendationService;
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

        // 3. Provjeri bolest — uzmi zadnju detekciju
        var lastDetection = await _diseaseDetectionRepo.GetLatestByPlantIdAsync(plant.Id);
        Disease? activeDisease = null;
        if (lastDetection is not null && !lastDetection.IsHealthy && lastDetection.DiseaseId.HasValue)
            activeDisease = await _diseaseRepo.GetByIdAsync(lastDetection.DiseaseId.Value);

        // 4. Rule engine — odluka o zalijevanju
        var sinceUtc = DateTime.UtcNow.AddHours(-24);

        var wateringCountLast24h =
            await _wateringRepo.CountNonSkippedByPotIdSinceAsync(
                request.PotId,
                sinceUtc);

        var decision = _ruleEngine.Evaluate(new WateringRuleInput
        {
            PlantId = plant.Id,
            CurrentSoilMoisture = request.SoilMoisture,
            MinRecommendedSoilMoisture = plant.PlantType.MinSoilMoisture,
            MaxRecommendedSoilMoisture = plant.PlantType.MaxSoilMoisture,
            CurrentTemperature = request.Temperature,
            MaxTemperatureNext24h = request.Temperature,
            CurrentLux = request.Lux,
            IsRainExposed = false,
            WeatherAvailable = false,
            RainExpectedIn24h = false,
            RainAmountNext24hMm = 0,
            RainIntensity = "none",
            HeatRiskNext24h = request.Temperature is >= 30,
            WateringCountLast24h = wateringCountLast24h,
            MaxWateringCountLast24h = 2,
            LocalNow = DateTime.Now
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
                WeatherSummary = "Automatska odluka na osnovu senzorskog očitanja. Vremenska prognoza nije korištena u SensorController toku.",
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
                SkipReason = decision.StatusMessage,
                IsForced = false,
                DecisionReason = decision.DecisionReason,
                WeatherSummary = decision.WeatherImpactMessage,
                CreatedAt = DateTime.UtcNow
            });
        }

        return Ok(new
        {
            message = "Očitavanje sačuvano.",
            wateringDecision = decision
        });
    }
}