using Bashta.Core.Entities;

namespace Bashta.Core.Interfaces;

public interface IWateringEventRepository
{
    Task<List<WateringEvent>> GetByPotIdAsync(int potId, int limit = 20);

    Task<WateringEvent?> GetLatestByPotIdAsync(int potId);

    Task<int> CountNonSkippedByPotIdSinceAsync(int potId, DateTime sinceUtc);

    Task<WateringEvent> CreateAsync(WateringEvent wateringEvent);
}