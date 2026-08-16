using Microsoft.AspNetCore.Http;

namespace Bashta.API.Requests;

public class DiseaseAnalyzeRequest
{
    public int PlantId { get; set; }
    public IFormFile Image { get; set; } = null!;
}