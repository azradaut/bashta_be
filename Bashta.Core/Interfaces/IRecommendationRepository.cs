using Bashta.Core.Entities;

namespace Bashta.Core.Interfaces;

public interface IRecommendationRepository
{
    Task<IEnumerable<Recommendation>> GetByPlantIdAsync(int plantId, bool unreadOnly = false);
    Task<Recommendation> CreateAsync(Recommendation recommendation);
    Task MarkAsReadAsync(int id);
}