using System;
using System.Collections.Generic;
using System.Text;

namespace Bashta.ML.Services;

public class TomatoDiseasePredictionResult
{
    public string Label { get; set; } = string.Empty;
    public string DiseaseName { get; set; } = string.Empty;
    public string? DiseaseNameLocal { get; set; }
    public float Confidence { get; set; }
    public bool IsHealthy { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}
