using Bashta.Core.Entities;

namespace Bashta.Core.Interfaces;

public interface IWateringEventRepository
{
    Task<IEnumerable<WateringEvent>> GetByPotIdAsync(int potId, int limit = 20);
    Task<WateringEvent?> GetLatestByPotIdAsync(int potId);
    Task<WateringEvent> CreateAsync(WateringEvent wateringEvent);
}