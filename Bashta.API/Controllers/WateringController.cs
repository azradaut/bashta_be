using Bashta.Core.DTOs;
using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WateringController : ControllerBase
{
    private readonly IWateringEventRepository _wateringRepo;

    public WateringController(IWateringEventRepository wateringRepo)
    {
        _wateringRepo = wateringRepo;
    }

    [HttpGet("{potId}")]
    public async Task<IActionResult> GetHistory(int potId, [FromQuery] int limit = 20)
    {
        var events = await _wateringRepo.GetByPotIdAsync(potId, limit);
        return Ok(events.Select(e => new WateringEventResponse
        {
            Id = e.Id,
            PotId = e.PotId,
            TriggeredBy = e.TriggeredBy,
            DurationSec = e.DurationSec,
            AmountMl = e.AmountMl,
            SoilMoistureBefore = e.SoilMoistureBefore,
            SoilMoistureAfter = e.SoilMoistureAfter,
            Skipped = e.Skipped,
            SkipReason = e.SkipReason,
            CreatedAt = e.CreatedAt
        }));
    }

    [HttpPost("manual")]
    public async Task<IActionResult> ManualWater([FromBody] ManualWateringRequest request)
    {
        var wateringEvent = new WateringEvent
        {
            PotId = request.PotId,
            TriggeredBy = "manual",
            DurationSec = request.DurationSec,
            AmountMl = request.AmountMl,
            Skipped = false
        };

        var created = await _wateringRepo.CreateAsync(wateringEvent);
        return Ok(new WateringEventResponse
        {
            Id = created.Id,
            PotId = created.PotId,
            TriggeredBy = created.TriggeredBy,
            DurationSec = created.DurationSec,
            AmountMl = created.AmountMl,
            Skipped = created.Skipped,
            CreatedAt = created.CreatedAt
        });
    }
}