using Bashta.Core.Entities;

namespace Bashta.Core.Interfaces;

public interface ISensorReadingRepository
{
    Task<IEnumerable<SensorReading>> GetByPotIdAsync(int potId, DateTime from, DateTime to);
    Task<SensorReading?> GetLatestByPotIdAsync(int potId);
    Task AddAsync(SensorReading reading);
}