using Bashta.Core.Entities;
using Bashta.Core.Interfaces;
using Bashta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bashta.Infrastructure.Repositories;

public class DiseaseRepository : IDiseaseRepository
{
    private readonly BashtaDbContext _context;

    public DiseaseRepository(BashtaDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Disease>> GetAllAsync() =>
        await _context.Diseases.ToListAsync();

    public async Task<Disease?> GetByIdAsync(int id) =>
        await _context.Diseases.FindAsync(id);
}