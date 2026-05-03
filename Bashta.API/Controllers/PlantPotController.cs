using Bashta.Core.DTOs;
using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlantPotController : ControllerBase
{
    private readonly IPlantPotRepository _potRepo;

    public PlantPotController(IPlantPotRepository potRepo)
    {
        _potRepo = potRepo;
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetByUser(int userId)
    {
        var pots = await _potRepo.GetByUserIdAsync(userId);
        var response = pots.Select(p => new PlantPotResponse
        {
            Id = p.Id,
            Name = p.Name,
            Location = p.Location,
            MacAddress = p.MacAddress,
            FirmwareVersion = p.FirmwareVersion,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            Plants = p.Plants.Select(pl => new PlantSummary
            {
                Id = pl.Id,
                Nickname = pl.Nickname,
                PlantTypeName = pl.PlantType.NameLocal ?? pl.PlantType.Name,
                PlantedAt = pl.PlantedAt
            }).ToList()
        });

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var pot = await _potRepo.GetByIdAsync(id);
        if (pot is null) return NotFound();

        return Ok(new PlantPotResponse
        {
            Id = pot.Id,
            Name = pot.Name,
            Location = pot.Location,
            MacAddress = pot.MacAddress,
            FirmwareVersion = pot.FirmwareVersion,
            IsActive = pot.IsActive,
            CreatedAt = pot.CreatedAt,
            Plants = pot.Plants.Select(pl => new PlantSummary
            {
                Id = pl.Id,
                Nickname = pl.Nickname,
                PlantTypeName = pl.PlantType.NameLocal ?? pl.PlantType.Name,
                PlantedAt = pl.PlantedAt
            }).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PlantPotRequest request, [FromQuery] int userId)
    {
        var pot = new PlantPot
        {
            UserId = userId,
            Name = request.Name,
            Location = request.Location,
            MacAddress = request.MacAddress,
            FirmwareVersion = request.FirmwareVersion
        };

        var created = await _potRepo.CreateAsync(pot);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.Id);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] PlantPotRequest request)
    {
        var pot = await _potRepo.GetByIdAsync(id);
        if (pot is null) return NotFound();

        pot.Name = request.Name;
        pot.Location = request.Location;
        pot.MacAddress = request.MacAddress;
        pot.FirmwareVersion = request.FirmwareVersion;

        await _potRepo.UpdateAsync(pot);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _potRepo.DeleteAsync(id);
        return NoContent();
    }
}