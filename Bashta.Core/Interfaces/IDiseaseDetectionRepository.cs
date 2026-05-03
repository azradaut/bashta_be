using Bashta.Core.Entities;

namespace Bashta.Core.Interfaces;

public interface IDiseaseDetectionRepository
{
    Task<IEnumerable<DiseaseDetection>> GetByPlantIdAsync(int plantId, int limit = 10);
    Task<DiseaseDetection?> GetLatestByPlantIdAsync(int plantId);
    Task<DiseaseDetection> CreateAsync(DiseaseDetection detection);
}