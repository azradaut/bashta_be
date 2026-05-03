using Bashta.Core.Entities;

namespace Bashta.Core.Interfaces;

public interface IPlantRepository
{
    Task<Plant?> GetByIdAsync(int id);
    Task<Plant?> GetActiveByPotIdAsync(int potId);
    Task<Plant> CreateAsync(Plant plant);
    Task<Plant> UpdateAsync(Plant plant);
}