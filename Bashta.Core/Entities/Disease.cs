namespace Bashta.Core.Entities;

public class Disease
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameLocal { get; set; }
    public decimal WateringModifier { get; set; } = 1.00m;
    public string? Description { get; set; }
    public string? TreatmentRecommendation { get; set; }

    // Navigation
    public ICollection<DiseaseDetection> DiseaseDetections { get; set; } = new List<DiseaseDetection>();
}