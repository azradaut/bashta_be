using Bashta.Core.DTOs;
using Bashta.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecommendationController : ControllerBase
{
    private readonly IRecommendationRepository _recommendationRepo;

    public RecommendationController(IRecommendationRepository recommendationRepo)
    {
        _recommendationRepo = recommendationRepo;
    }

    [HttpGet("{plantId}")]
    public async Task<IActionResult> GetByPlant(int plantId, [FromQuery] bool unreadOnly = false)
    {
        var recs = await _recommendationRepo.GetByPlantIdAsync(plantId, unreadOnly);
        return Ok(recs.Select(r => new RecommendationResponse
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
        await _recommendationRepo.MarkAsReadAsync(id);
        return NoContent();
    }
}