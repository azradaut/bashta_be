using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bashta.Infrastructure.Repositories;

public class PlantPotRepository : IPlantPotRepository
{
    private readonly BashtaDbContext _context;

    public PlantPotRepository(
        BashtaDbContext context)
    {
        _context = context;
    }

    public async Task<PlantPot?> GetByIdAsync(
        int id)
    {
        return await _context.PlantPots
            .Include(p => p.Plants)
            .ThenInclude(pl => pl.PlantType)
            .FirstOrDefaultAsync(
                p => p.Id == id);
    }

    public async Task<IEnumerable<PlantPot>>
        GetByUserIdAsync(
            int userId)
    {
        return await _context.PlantPots
            .Where(p =>
                p.UserId == userId)
            .Include(p => p.Plants)
            .ThenInclude(pl => pl.PlantType)
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<PlantPot> CreateAsync(
        PlantPot pot)
    {
        _context.PlantPots.Add(pot);

        await _context.SaveChangesAsync();

        return pot;
    }

    public async Task<PlantPot> UpdateAsync(
        PlantPot pot)
    {
        _context.PlantPots.Attach(pot);

        _context.Entry(pot)
            .Property(p => p.Name)
            .IsModified = true;

        _context.Entry(pot)
            .Property(p => p.Location)
            .IsModified = true;

        _context.Entry(pot)
            .Property(p => p.MacAddress)
            .IsModified = true;

        _context.Entry(pot)
            .Property(p => p.FirmwareVersion)
            .IsModified = true;

        _context.Entry(pot)
            .Property(p => p.IsActive)
            .IsModified = true;

        _context.Entry(pot)
            .Property(p => p.IsRainExposed)
            .IsModified = true;

        _context.Entry(pot)
            .Property(
                p => p.SensorReadingIntervalMinutes)
            .IsModified = true;

        await _context.SaveChangesAsync();

        return pot;
    }

    public async Task DeleteAsync(
        int id)
    {
        var pot =
            await _context.PlantPots
                .FirstOrDefaultAsync(
                    p => p.Id == id);

        if (pot is null)
            return;

        _context.PlantPots.Remove(pot);

        await _context.SaveChangesAsync();
    }
}