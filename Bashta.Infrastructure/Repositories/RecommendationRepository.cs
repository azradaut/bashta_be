using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bashta.Infrastructure.Repositories;

public class RecommendationRepository : IRecommendationRepository
{
    private readonly BashtaDbContext _context;

    public RecommendationRepository(BashtaDbContext context)
    {
        _context = context;
    }

    public async Task<Recommendation?> GetByIdAsync(int id) =>
        await _context.Recommendations
            .Include(r => r.Plant)
                .ThenInclude(p => p.PlantPot)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IEnumerable<Recommendation>> GetByPlantIdAsync(int plantId, bool unreadOnly = false) =>
        await _context.Recommendations
            .Where(r => r.PlantId == plantId && (!unreadOnly || !r.IsRead))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public async Task<Recommendation> CreateAsync(Recommendation recommendation)
    {
        _context.Recommendations.Add(recommendation);
        await _context.SaveChangesAsync();
        return recommendation;
    }

    public async Task MarkAsReadAsync(int id)
    {
        var recommendation = await _context.Recommendations.FindAsync(id);

        if (recommendation is null)
            return;

        recommendation.IsRead = true;
        await _context.SaveChangesAsync();
    }
}