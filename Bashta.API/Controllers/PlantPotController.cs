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

        var response = pots
            .Select(MapToResponse)
            .ToList();

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var pot = await _potRepo.GetByIdAsync(id);

        if (pot is null)
            return NotFound();

        return Ok(MapToResponse(pot));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] PlantPotRequest request,
        [FromQuery] int userId)
    {
        if (userId <= 0)
            return BadRequest("UserId je obavezan.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Naziv saksije je obavezan.");

        var pot = new PlantPot
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Location = string.IsNullOrWhiteSpace(request.Location)
                ? null
                : request.Location.Trim(),
            MacAddress = string.IsNullOrWhiteSpace(request.MacAddress)
                ? null
                : request.MacAddress.Trim(),
            FirmwareVersion = string.IsNullOrWhiteSpace(request.FirmwareVersion)
                ? null
                : request.FirmwareVersion.Trim(),

                IsRainExposed = request.IsRainExposed,
            SensorReadingIntervalMinutes = request.SensorReadingIntervalMinutes <= 0
    ? 60
    : request.SensorReadingIntervalMinutes
        };

        var created = await _potRepo.CreateAsync(pot);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            MapToResponse(created));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] PlantPotRequest request)
    {
        var pot = await _potRepo.GetByIdAsync(id);

        if (pot is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Naziv saksije je obavezan.");

        pot.Name = request.Name.Trim();
        pot.Location = string.IsNullOrWhiteSpace(request.Location)
            ? null
            : request.Location.Trim();
        pot.MacAddress = string.IsNullOrWhiteSpace(request.MacAddress)
            ? null
            : request.MacAddress.Trim();
        pot.FirmwareVersion = string.IsNullOrWhiteSpace(request.FirmwareVersion)
            ? null
            : request.FirmwareVersion.Trim();

        pot.IsRainExposed = request.IsRainExposed;

        pot.SensorReadingIntervalMinutes = request.SensorReadingIntervalMinutes <= 0
            ? 60
            : request.SensorReadingIntervalMinutes;

        await _potRepo.UpdateAsync(pot);

        return NoContent();

    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _potRepo.DeleteAsync(id);

        return NoContent();
    }

    private static PlantPotResponse MapToResponse(PlantPot pot)
    {
        return new PlantPotResponse
        {
            Id = pot.Id,
            Name = pot.Name,
            Location = pot.Location,
            MacAddress = pot.MacAddress,
            FirmwareVersion = pot.FirmwareVersion,
            IsActive = pot.IsActive,
            CreatedAt = pot.CreatedAt,
            IsRainExposed = pot.IsRainExposed,
            SensorReadingIntervalMinutes = pot.SensorReadingIntervalMinutes,

            Plants = pot.Plants
                .Where(pl => pl.RemovedAt == null)
                .OrderByDescending(pl => pl.PlantedAt)
                .ThenByDescending(pl => pl.Id)
                .Select(pl => new PlantSummary
                {
                    Id = pl.Id,
                    PlantTypeId = pl.PlantTypeId,
                    Nickname = pl.Nickname,
                    PlantTypeName = pl.PlantType.NameLocal ?? pl.PlantType.Name,
                    PlantedAt = pl.PlantedAt
                })
                .ToList()
        };
    }
}