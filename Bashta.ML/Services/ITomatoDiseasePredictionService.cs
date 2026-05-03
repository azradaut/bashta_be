using System;
using System.Collections.Generic;
using System.Text;

namespace Bashta.ML.Services;

public interface ITomatoDiseasePredictionService
{
    Task<TomatoDiseasePredictionResult> PredictAsync(Stream imageStream, CancellationToken cancellationToken = default);
}