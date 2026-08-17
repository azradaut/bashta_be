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

    public async Task<List<WateringEvent>> GetByPotIdAsync(int potId, int limit = 20)
    {
        return await _context.WateringEvents
            .Where(w => w.PotId == potId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<WateringEvent?> GetLatestByPotIdAsync(int potId)
    {
        return await _context.WateringEvents
            .Where(w => w.PotId == potId && !w.Skipped)
            .OrderByDescending(w => w.CreatedAt)
            .FirstOrDefaultAsync();
    }
    public async Task<int> CountManualNonSkippedByPotIdSinceAsync(
    int potId,
    DateTime sinceUtc)
    {
        return await _context.WateringEvents
            .CountAsync(x =>
                x.PotId == potId &&
                x.CreatedAt >= sinceUtc &&
                !x.Skipped &&
                x.TriggeredBy == "manual");
    }

    public async Task<int> CountNonSkippedByPotIdSinceAsync(int potId, DateTime sinceUtc)
    {
        return await _context.WateringEvents
            .CountAsync(w =>
                w.PotId == potId &&
                !w.Skipped &&
                w.CreatedAt >= sinceUtc);
    }

    public async Task<WateringEvent> CreateAsync(WateringEvent wateringEvent)
    {
        _context.WateringEvents.Add(wateringEvent);
        await _context.SaveChangesAsync();

        return wateringEvent;
    }
}