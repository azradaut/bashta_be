using Bashta.Core.Entities;

namespace Bashta.Core.Interfaces;

public interface IWateringEventRepository
{
    Task<List<WateringEvent>> GetByPotIdAsync(
        int potId,
        int limit = 20);

    Task<WateringEvent?> GetLatestByPotIdAsync(
        int potId);

    Task<int> CountNonSkippedByPotIdSinceAsync(
        int potId,
        DateTime sinceUtc);

    Task<int> CountManualNonSkippedByPotIdSinceAsync(
        int potId,
        DateTime sinceUtc);
    Task<List<WateringEvent>> GetRecentCompletedByPotIdAsync(
        int potId,
        int limit = 5);

    Task<WateringEvent> CreateAsync(
        WateringEvent wateringEvent);
}