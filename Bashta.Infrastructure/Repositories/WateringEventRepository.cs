using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bashta.Infrastructure.Repositories;

public class WateringEventRepository : IWateringEventRepository
{
    private readonly BashtaDbContext _context;

    public WateringEventRepository(BashtaDbContext context) => _context = context;

    public async Task<List<WateringEvent>> GetByPotIdAsync(int potId, int limit = 20) =>
        await _context.WateringEvents
            .AsNoTracking()
            .Where(w => w.PotId == potId && !(w.TriggeredBy == "manual" && w.DurationSec == 0 && !w.Skipped))
            .OrderByDescending(w => w.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<WateringEvent?> GetLatestByPotIdAsync(int potId) =>
        await _context.WateringEvents
            .AsNoTracking()
            .Where(w => w.PotId == potId && !w.Skipped && w.DurationSec.HasValue && w.DurationSec.Value > 0)
            .OrderByDescending(w => w.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<int> CountNonSkippedByPotIdSinceAsync(int potId, DateTime sinceUtc) =>
        await _context.WateringEvents.CountAsync(w =>
            w.PotId == potId &&
            w.CreatedAt >= sinceUtc &&
            !w.Skipped &&
            w.DurationSec.HasValue &&
            w.DurationSec.Value > 0);

    public async Task<int> CountManualNonSkippedByPotIdSinceAsync(int potId, DateTime sinceUtc) =>
        await _context.WateringEvents.CountAsync(w =>
            w.PotId == potId &&
            w.CreatedAt >= sinceUtc &&
            w.TriggeredBy == "manual" &&
            !w.Skipped &&
            w.DurationSec.HasValue &&
            w.DurationSec.Value > 0);

    public async Task<List<WateringEvent>> GetRecentCompletedByPotIdAsync(int potId, int limit = 5) =>
        await _context.WateringEvents
            .AsNoTracking()
            .Where(w => w.PotId == potId && !w.Skipped && w.DurationSec.HasValue && w.DurationSec.Value > 0)
            .OrderByDescending(w => w.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<WateringEvent?> GetPendingManualByPotIdAsync(int potId) =>
        await _context.WateringEvents
            .Where(w => w.PotId == potId && w.TriggeredBy == "manual" && !w.Skipped && w.DurationSec == 0)
            .OrderBy(w => w.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<WateringEvent?> GetByIdAsync(int id) =>
        await _context.WateringEvents.FirstOrDefaultAsync(w => w.Id == id);

    public async Task<WateringEvent> CreateAsync(WateringEvent wateringEvent)
    {
        _context.WateringEvents.Add(wateringEvent);
        await _context.SaveChangesAsync();
        return wateringEvent;
    }

    public async Task<WateringEvent> UpdateAsync(WateringEvent wateringEvent)
    {
        if (_context.Entry(wateringEvent).State == EntityState.Detached)
        {
            var tracked = await _context.WateringEvents.FirstOrDefaultAsync(w => w.Id == wateringEvent.Id)
                ?? throw new InvalidOperationException($"WateringEvent {wateringEvent.Id} nije pronađen.");

            tracked.DurationSec = wateringEvent.DurationSec;
            tracked.AmountMl = wateringEvent.AmountMl;
            tracked.SoilMoistureBefore = wateringEvent.SoilMoistureBefore;
            tracked.SoilMoistureAfter = wateringEvent.SoilMoistureAfter;
            tracked.Skipped = wateringEvent.Skipped;
            tracked.SkipReason = wateringEvent.SkipReason;
            tracked.IsForced = wateringEvent.IsForced;
            tracked.DecisionReason = wateringEvent.DecisionReason;
            tracked.WeatherSummary = wateringEvent.WeatherSummary;
            wateringEvent = tracked;
        }

        await _context.SaveChangesAsync();
        return wateringEvent;
    }
}
