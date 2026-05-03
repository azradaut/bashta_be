namespace Bashta.Core.DTOs;

public class DiseaseDetectionResponse
{
    public int Id { get; set; }
    public int PlantId { get; set; }
    public string? DiseaseName { get; set; }
    public string? DiseaseNameLocal { get; set; }
    public decimal? Confidence { get; set; }
    public bool IsHealthy { get; set; }
    public string? TreatmentRecommendation { get; set; }
    public DateTime CreatedAt { get; set; }
}