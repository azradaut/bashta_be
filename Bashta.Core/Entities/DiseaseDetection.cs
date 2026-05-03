namespace Bashta.Core.Entities;

public class DiseaseDetection
{
    public int Id { get; set; }
    public int PlantId { get; set; }
    public int? DiseaseId { get; set; }
    public decimal? Confidence { get; set; }
    public string? ImagePath { get; set; }
    public bool IsHealthy { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Plant Plant { get; set; } = null!;
    public Disease? Disease { get; set; }
}