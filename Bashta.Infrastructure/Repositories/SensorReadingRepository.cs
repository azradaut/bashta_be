using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bashta.Infrastructure.Repositories;

public class SensorReadingRepository : ISensorReadingRepository
{
    private readonly BashtaDbContext _context;

    public SensorReadingRepository(BashtaDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SensorReading>> GetByPotIdAsync(int potId, DateTime from, DateTime to) =>
        await _context.SensorReadings
            .Where(s => s.PotId == potId && s.Time >= from && s.Time <= to)
            .OrderBy(s => s.Time)
            .ToListAsync();

    public async Task<SensorReading?> GetLatestByPotIdAsync(int potId) =>
        await _context.SensorReadings
            .Where(s => s.PotId == potId)
            .OrderByDescending(s => s.Time)
            .FirstOrDefaultAsync();

    public async Task AddAsync(SensorReading reading)
    {
        _context.SensorReadings.Add(reading);
        await _context.SaveChangesAsync();
    }
}