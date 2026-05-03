using Bashta.API.Requests;
using Bashta.Core.DTOs;
using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.ML.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiseaseController : ControllerBase
{
    private readonly IDiseaseDetectionRepository _detectionRepo;
    private readonly IDiseaseRepository _diseaseRepo;
    private readonly IPlantRepository _plantRepo;
    private readonly ITomatoDiseasePredictionService _tomatoPredictionService;
    private readonly IWebHostEnvironment _environment;

    public DiseaseController(
        IDiseaseDetectionRepository detectionRepo,
        IDiseaseRepository diseaseRepo,
        IPlantRepository plantRepo,
        ITomatoDiseasePredictionService tomatoPredictionService,
        IWebHostEnvironment environment)
    {
        _detectionRepo = detectionRepo;
        _diseaseRepo = diseaseRepo;
        _plantRepo = plantRepo;
        _tomatoPredictionService = tomatoPredictionService;
        _environment = environment;
    }

    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DiseaseDetectionResponse>> Analyze(
        [FromForm] DiseaseAnalyzeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Image is null || request.Image.Length == 0)
            return BadRequest("Slika je obavezna.");

        var plant = await _plantRepo.GetByIdAsync(request.PlantId);
        if (plant is null)
            return NotFound($"Biljka sa ID {request.PlantId} nije pronađena.");

        var extension = Path.GetExtension(request.Image.FileName).ToLowerInvariant();

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExtensions.Contains(extension))
            return BadRequest("Dozvoljeni formati slike su JPG, JPEG, PNG i WEBP.");

        var webRootPath = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
        }

        var uploadsDirectory = Path.Combine(
            webRootPath,
            "uploads",
            "disease-detections"
        );

        Directory.CreateDirectory(uploadsDirectory);

        var fileName =
            $"{request.PlantId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension}";

        var absoluteImagePath = Path.Combine(uploadsDirectory, fileName);

        await using (var fileStream = System.IO.File.Create(absoluteImagePath))
        {
            await request.Image.CopyToAsync(fileStream, cancellationToken);
        }

        TomatoDiseasePredictionResult prediction;

        await using (var imageStream = System.IO.File.OpenRead(absoluteImagePath))
        {
            prediction = await _tomatoPredictionService.PredictAsync(
                imageStream,
                cancellationToken
            );
        }

        Disease? disease = null;

        if (!prediction.IsHealthy)
        {
            disease = await _diseaseRepo.GetByNameAsync(prediction.DiseaseName);
        }

        var relativeImagePath = $"/uploads/disease-detections/{fileName}";

        var detection = new DiseaseDetection
        {
            PlantId = request.PlantId,
            DiseaseId = disease?.Id,
            Confidence = Math.Round((decimal)prediction.Confidence, 4),
            ImagePath = relativeImagePath,
            IsHealthy = prediction.IsHealthy,
            CreatedAt = DateTime.UtcNow
        };

        var savedDetection = await _detectionRepo.CreateAsync(detection);

        return Ok(new DiseaseDetectionResponse
        {
            Id = savedDetection.Id,
            PlantId = savedDetection.PlantId,
            DiseaseName = disease?.Name ?? prediction.DiseaseName,
            DiseaseNameLocal = disease?.NameLocal ?? prediction.DiseaseNameLocal,
            Confidence = savedDetection.Confidence,
            IsHealthy = savedDetection.IsHealthy,
            TreatmentRecommendation = disease?.TreatmentRecommendation ?? prediction.Recommendation,
            ImagePath = savedDetection.ImagePath,
            CreatedAt = savedDetection.CreatedAt
        });
    }

    [HttpGet("history/{plantId}")]
    public async Task<ActionResult<IEnumerable<DiseaseDetectionResponse>>> GetHistory(
        int plantId,
        [FromQuery] int limit = 10)
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
            ImagePath = d.ImagePath,
            CreatedAt = d.CreatedAt
        }));
    }

    [HttpGet("latest/{plantId}")]
    public async Task<ActionResult<DiseaseDetectionResponse>> GetLatest(int plantId)
    {
        var detection = await _detectionRepo.GetLatestByPlantIdAsync(plantId);

        if (detection is null)
            return NotFound();

        return Ok(new DiseaseDetectionResponse
        {
            Id = detection.Id,
            PlantId = detection.PlantId,
            DiseaseName = detection.Disease?.Name,
            DiseaseNameLocal = detection.Disease?.NameLocal,
            Confidence = detection.Confidence,
            IsHealthy = detection.IsHealthy,
            TreatmentRecommendation = detection.Disease?.TreatmentRecommendation,
            ImagePath = detection.ImagePath,
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