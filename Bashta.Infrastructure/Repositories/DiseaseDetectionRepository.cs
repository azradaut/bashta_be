using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bashta.Infrastructure.Repositories;

public class DiseaseDetectionRepository : IDiseaseDetectionRepository
{
    private readonly BashtaDbContext _context;

    public DiseaseDetectionRepository(BashtaDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<DiseaseDetection>> GetByPlantIdAsync(int plantId, int limit = 10) =>
        await _context.DiseaseDetections
            .Where(d => d.PlantId == plantId)
            .Include(d => d.Disease)
            .OrderByDescending(d => d.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<DiseaseDetection?> GetLatestByPlantIdAsync(int plantId) =>
        await _context.DiseaseDetections
            .Where(d => d.PlantId == plantId)
            .Include(d => d.Disease)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<DiseaseDetection> CreateAsync(DiseaseDetection detection)
    {
        _context.DiseaseDetections.Add(detection);
        await _context.SaveChangesAsync();
        return detection;
    }
}