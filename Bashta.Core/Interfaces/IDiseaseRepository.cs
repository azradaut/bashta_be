using Bashta.Core.Entities;

namespace Bashta.Core.Interfaces;

public interface IDiseaseRepository
{
    Task<List<Disease>> GetAllAsync();
    Task<Disease?> GetByIdAsync(int id);
    Task<Disease?> GetByNameAsync(string name);
}