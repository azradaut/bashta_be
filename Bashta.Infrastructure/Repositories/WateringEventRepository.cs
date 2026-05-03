using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bashta.Infrastructure.Repositories;

public class WateringEventRepository : IWateringEventRepository
{
    private readonly BashtaDbContext _context;

    public WateringEventRepository(BashtaDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<WateringEvent>> GetByPotIdAsync(int potId, int limit = 20) =>
        await _context.WateringEvents
            .Where(w => w.PotId == potId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<WateringEvent?> GetLatestByPotIdAsync(int potId) =>
        await _context.WateringEvents
            .Where(w => w.PotId == potId && !w.Skipped)
            .OrderByDescending(w => w.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<WateringEvent> CreateAsync(WateringEvent wateringEvent)
    {
        _context.WateringEvents.Add(wateringEvent);
        await _context.SaveChangesAsync();
        return wateringEvent;
    }
}