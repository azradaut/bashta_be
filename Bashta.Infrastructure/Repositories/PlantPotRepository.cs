using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bashta.Infrastructure.Repositories;

public class PlantPotRepository : IPlantPotRepository
{
    private readonly BashtaDbContext _context;

    public PlantPotRepository(BashtaDbContext context)
    {
        _context = context;
    }

    public async Task<PlantPot?> GetByIdAsync(int id) =>
        await _context.PlantPots
            .Include(p => p.Plants)
            .ThenInclude(pl => pl.PlantType)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IEnumerable<PlantPot>> GetByUserIdAsync(int userId) =>
        await _context.PlantPots
            .Where(p => p.UserId == userId && p.IsActive)
            .Include(p => p.Plants)
            .ThenInclude(pl => pl.PlantType)
            .ToListAsync();

    public async Task<PlantPot> CreateAsync(PlantPot pot)
    {
        _context.PlantPots.Add(pot);
        await _context.SaveChangesAsync();
        return pot;
    }

    public async Task<PlantPot> UpdateAsync(PlantPot pot)
    {
        _context.PlantPots.Update(pot);
        await _context.SaveChangesAsync();
        return pot;
    }

    public async Task DeleteAsync(int id)
    {
        var pot = await _context.PlantPots.FindAsync(id);
        if (pot is not null)
        {
            pot.IsActive = false;  // soft delete
            await _context.SaveChangesAsync();
        }
    }
}