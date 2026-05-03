namespace Bashta.Core.DTOs;

public class RecommendationResponse
{
    public int Id { get; set; }
    public int PlantId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}