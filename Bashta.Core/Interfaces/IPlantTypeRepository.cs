using Bashta.Core.Entities;

namespace Bashta.Core.Interfaces;

public interface IPlantTypeRepository
{
    Task<IEnumerable<PlantType>> GetAllAsync();
    Task<PlantType?> GetByIdAsync(int id);
}