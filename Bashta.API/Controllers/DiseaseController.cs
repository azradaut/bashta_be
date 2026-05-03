using Bashta.Core.DTOs;
using Bashta.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiseaseController : ControllerBase
{
    private readonly IDiseaseDetectionRepository _detectionRepo;
    private readonly IDiseaseRepository _diseaseRepo;

    public DiseaseController(
        IDiseaseDetectionRepository detectionRepo,
        IDiseaseRepository diseaseRepo)
    {
        _detectionRepo = detectionRepo;
        _diseaseRepo = diseaseRepo;
    }

    [HttpGet("history/{plantId}")]
    public async Task<IActionResult> GetHistory(int plantId, [FromQuery] int limit = 10)
    {
        var detections = await _detectionRepo.GetByPlantIdAsync(plantId, limit);
        return Ok(detections.Select(d => new DiseaseDetectionResponse
        {
            Id = d.Id,
            PlantId = d.PlantId,
            DiseaseName = d.Disease?.Name,
            DiseaseNameLocal = d.Disease?.NameLocal,
            Confidence = d.Confidence,
            IsHealthy = d.IsHealthy,
            TreatmentRecommendation = d.Disease?.TreatmentRecommendation,
            CreatedAt = d.CreatedAt
        }));
    }

    [HttpGet("latest/{plantId}")]
    public async Task<IActionResult> GetLatest(int plantId)
    {
        var detection = await _detectionRepo.GetLatestByPlantIdAsync(plantId);
        if (detection is null) return NotFound();

        return Ok(new DiseaseDetectionResponse
        {
            Id = detection.Id,
            PlantId = detection.PlantId,
            DiseaseName = detection.Disease?.Name,
            DiseaseNameLocal = detection.Disease?.NameLocal,
            Confidence = detection.Confidence,
            IsHealthy = detection.IsHealthy,
            TreatmentRecommendation = detection.Disease?.TreatmentRecommendation,
            CreatedAt = detection.CreatedAt
        });
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog()
    {
        var diseases = await _diseaseRepo.GetAllAsync();
        return Ok(diseases);
    }
}