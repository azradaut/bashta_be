using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bashta.Infrastructure.Repositories;

public class PlantTypeRepository : IPlantTypeRepository
{
    private readonly BashtaDbContext _context;

    public PlantTypeRepository(BashtaDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<PlantType>> GetAllAsync() =>
        await _context.PlantTypes.ToListAsync();

    public async Task<PlantType?> GetByIdAsync(int id) =>
        await _context.PlantTypes.FindAsync(id);
}