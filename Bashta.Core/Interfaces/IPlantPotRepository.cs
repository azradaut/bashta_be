using Bashta.Core.Entities;

namespace Bashta.Core.Interfaces;

public interface IPlantPotRepository
{
    Task<PlantPot?> GetByIdAsync(int id);
    Task<IEnumerable<PlantPot>> GetByUserIdAsync(int userId);
    Task<PlantPot> CreateAsync(PlantPot pot);
    Task<PlantPot> UpdateAsync(PlantPot pot);
    Task DeleteAsync(int id);
}