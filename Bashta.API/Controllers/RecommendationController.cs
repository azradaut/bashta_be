using System.Security.Claims;
using Bashta.Core.DTOs;
using Bashta.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecommendationController : ControllerBase
{
    private readonly IRecommendationRepository _recommendationRepo;
    private readonly IPlantRepository _plantRepo;

    public RecommendationController(IRecommendationRepository recommendationRepo, IPlantRepository plantRepo)
    {
        _recommendationRepo = recommendationRepo;
        _plantRepo = plantRepo;
    }

    [HttpGet("{plantId}")]
    public async Task<IActionResult> GetByPlant(int plantId, [FromQuery] bool unreadOnly = false)
    {
        var plant = await _plantRepo.GetByIdAsync(plantId);

        if (plant is null)
            return NotFound();

        if (plant.PlantPot.UserId != GetCurrentUserId())
            return Forbid();

        var recommendations = await _recommendationRepo.GetByPlantIdAsync(plantId, unreadOnly);

        return Ok(recommendations.Select(r => new RecommendationResponse
        {
            Id = r.Id,
            PlantId = r.PlantId,
            Type = r.Type,
            Message = r.Message,
            IsRead = r.IsRead,
            Source = r.Source,
            CreatedAt = r.CreatedAt
        }));
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var recommendation = await _recommendationRepo.GetByIdAsync(id);

        if (recommendation is null)
            return NotFound();

        if (recommendation.Plant.PlantPot.UserId != GetCurrentUserId())
            return Forbid();

        await _recommendationRepo.MarkAsReadAsync(id);

        return NoContent();
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(value, out var userId))
            throw new UnauthorizedAccessException();

        return userId;
    }
}