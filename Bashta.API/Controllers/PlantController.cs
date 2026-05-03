using Bashta.Core.DTOs;
using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlantController : ControllerBase
{
    private readonly IPlantRepository _plantRepo;
    private readonly IPlantTypeRepository _plantTypeRepo;

    public PlantController(IPlantRepository plantRepo, IPlantTypeRepository plantTypeRepo)
    {
        _plantRepo = plantRepo;
        _plantTypeRepo = plantTypeRepo;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var plant = await _plantRepo.GetByIdAsync(id);
        if (plant is null) return NotFound();

        return Ok(MapToResponse(plant));
    }

    [HttpGet("active/{potId}")]
    public async Task<IActionResult> GetActiveByPot(int potId)
    {
        var plant = await _plantRepo.GetActiveByPotIdAsync(potId);
        if (plant is null) return NotFound();

        return Ok(MapToResponse(plant));
    }

    [HttpGet("types")]
    public async Task<IActionResult> GetPlantTypes()
    {
        var types = await _plantTypeRepo.GetAllAsync();
        return Ok(types.Select(t => new PlantTypeDetail
        {
            Id = t.Id,
            Name = t.Name,
            NameLocal = t.NameLocal,
            MinSoilMoisture = t.MinSoilMoisture,
            MaxSoilMoisture = t.MaxSoilMoisture,
            MinTemp = t.MinTemp,
            MaxTemp = t.MaxTemp,
            TargetDli = t.TargetDli
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PlantRequest request)
    {
        var plant = new Plant
        {
            PotId = request.PotId,
            PlantTypeId = request.PlantTypeId,
            Nickname = request.Nickname,
            PlantedAt = request.PlantedAt ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Notes = request.Notes
        };

        var created = await _plantRepo.CreateAsync(plant);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.Id);
    }

    [HttpPatch("{id}/remove")]
    public async Task<IActionResult> RemovePlant(int id)
    {
        var plant = await _plantRepo.GetByIdAsync(id);
        if (plant is null) return NotFound();

        plant.RemovedAt = DateOnly.FromDateTime(DateTime.UtcNow);
        await _plantRepo.UpdateAsync(plant);
        return NoContent();
    }

    private static PlantResponse MapToResponse(Plant plant) => new()
    {
        Id = plant.Id,
        PotId = plant.PotId,
        Nickname = plant.Nickname,
        PlantedAt = plant.PlantedAt,
        Notes = plant.Notes,
        PlantType = new PlantTypeDetail
        {
            Id = plant.PlantType.Id,
            Name = plant.PlantType.Name,
            NameLocal = plant.PlantType.NameLocal,
            MinSoilMoisture = plant.PlantType.MinSoilMoisture,
            MaxSoilMoisture = plant.PlantType.MaxSoilMoisture,
            MinTemp = plant.PlantType.MinTemp,
            MaxTemp = plant.PlantType.MaxTemp,
            TargetDli = plant.PlantType.TargetDli
        }
    };
}