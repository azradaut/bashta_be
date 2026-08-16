namespace Bashta.Core.Entities;

public class Plant
{
    public int Id { get; set; }
    public int PotId { get; set; }
    public int PlantTypeId { get; set; }
    public string? Nickname { get; set; }
    public DateOnly PlantedAt { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? RemovedAt { get; set; }
    public string? Notes { get; set; }
    public string? ImagePath { get; set; }

    // Navigation
    public PlantPot PlantPot { get; set; } = null!;
    public PlantType PlantType { get; set; } = null!;
    public ICollection<DiseaseDetection> DiseaseDetections { get; set; } = new List<DiseaseDetection>();
    public ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
}