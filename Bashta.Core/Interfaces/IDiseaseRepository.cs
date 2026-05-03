using Bashta.Core.Entities;

namespace Bashta.Core.Interfaces;

public interface IDiseaseRepository
{
    Task<IEnumerable<Disease>> GetAllAsync();
    Task<Disease?> GetByIdAsync(int id);
}