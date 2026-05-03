using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bashta.Infrastructure.Repositories;

public class PlantRepository : IPlantRepository
{
    private readonly BashtaDbContext _context;

    public PlantRepository(BashtaDbContext context)
    {
        _context = context;
    }

    public async Task<Plant?> GetByIdAsync(int id) =>
        await _context.Plants
            .Include(p => p.PlantType)
            .Include(p => p.PlantPot)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Plant?> GetActiveByPotIdAsync(int potId) =>
        await _context.Plants
            .Include(p => p.PlantType)
            .Include(p => p.PlantPot)
            .Where(p => p.PotId == potId && p.RemovedAt == null)
            .FirstOrDefaultAsync();

    public async Task<Plant> CreateAsync(Plant plant)
    {
        _context.Plants.Add(plant);
        await _context.SaveChangesAsync();
        return plant;
    }

    public async Task<Plant> UpdateAsync(Plant plant)
    {
        _context.Plants.Update(plant);
        await _context.SaveChangesAsync();
        return plant;
    }
}