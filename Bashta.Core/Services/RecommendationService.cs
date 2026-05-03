using Bashta.Core.Entities;
using Bashta.Core.Interfaces;

namespace Bashta.Core.Services;

public class RecommendationService
{
    private readonly IRecommendationRepository _recommendationRepo;
    private readonly INotificationRepository _notificationRepo;

    public RecommendationService(
        IRecommendationRepository recommendationRepo,
        INotificationRepository notificationRepo)
    {
        _recommendationRepo = recommendationRepo;
        _notificationRepo = notificationRepo;
    }

    public async Task CreateWateringRecommendationAsync(int plantId, int userId, string message)
    {
        await _recommendationRepo.CreateAsync(new Recommendation
        {
            PlantId = plantId,
            Type = "watering",
            Message = message,
            Source = "rule_engine"
        });

        await _notificationRepo.CreateAsync(new Notification
        {
            UserId = userId,
            Title = "Potrebno zalijevanje",
            Body = message,
            Type = "watering"
        });
    }

    public async Task CreateDiseaseRecommendationAsync(
        int plantId, int userId, Disease disease, decimal confidence)
    {
        var message = $"Otkrivena bolest: {disease.NameLocal} " +
                      $"({confidence * 100:F1}% pouzdanost). " +
                      $"{disease.TreatmentRecommendation}";

        await _recommendationRepo.CreateAsync(new Recommendation
        {
            PlantId = plantId,
            Type = "disease",
            Message = message,
            Source = "ai"
        });

        await _notificationRepo.CreateAsync(new Notification
        {
            UserId = userId,
            Title = $"Otkrivena bolest: {disease.NameLocal}",
            Body = message,
            Type = "disease"
        });
    }

    public async Task CreateLightingRecommendationAsync(
        int plantId, int userId, double actualDli, double targetDli, string evaluation)
    {
        var message = $"DLI danas: {actualDli} mol/m² (cilj: {targetDli}). {evaluation}";

        await _recommendationRepo.CreateAsync(new Recommendation
        {
            PlantId = plantId,
            Type = "lighting",
            Message = message,
            Source = "rule_engine"
        });

        await _notificationRepo.CreateAsync(new Notification
        {
            UserId = userId,
            Title = "Upozorenje o svjetlosti",
            Body = message,
            Type = "alert"
        });
    }
}