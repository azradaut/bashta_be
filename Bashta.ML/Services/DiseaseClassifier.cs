using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace Bashta.ML.Services;

public class ClassificationResult
{
    public string ClassName { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public bool IsHealthy { get; set; }
}

public class DiseaseClassifier : IDisposable
{
    private readonly InferenceSession _session;
    private readonly List<string> _classNames;
    private const int ImageSize = 224;

    public DiseaseClassifier(string modelPath, string classNamesPath)
    {
        _session = new InferenceSession(modelPath);
        _classNames = File.ReadAllLines(classNamesPath)
                         .Where(l => !string.IsNullOrWhiteSpace(l))
                         .ToList();
    }

    public ClassificationResult Classify(Stream imageStream)
    {
        var tensor = PreprocessImage(imageStream);

        var inputName = _session.InputMetadata.Keys.First();
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, tensor)
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsEnumerable<float>().ToArray();

        // Softmax ako model ne vraća probabilitete
        var softmax = Softmax(output);
        var maxIndex = softmax
                            .Select((v, i) => (v, i))
                            .OrderByDescending(x => x.v)
                            .First();

        var className = _classNames[maxIndex.i];
        var confidence = maxIndex.v;
        var isHealthy = className.Contains("healthy", StringComparison.OrdinalIgnoreCase);

        return new ClassificationResult
        {
            ClassName = className,
            Confidence = confidence,
            IsHealthy = isHealthy
        };
    }

    private DenseTensor<float> PreprocessImage(Stream imageStream)
    {
        using var image = Image.Load<Rgb24>(imageStream);

        // Resize na 224x224
        image.Mutate(x => x.Resize(ImageSize, ImageSize));

        var tensor = new DenseTensor<float>(new[] { 1, 3, ImageSize, ImageSize });

        // Normalizacija — ImageNet mean/std
        float[] mean = { 0.485f, 0.456f, 0.406f };
        float[] std = { 0.229f, 0.224f, 0.225f };

        for (int y = 0; y < ImageSize; y++)
        {
            for (int x = 0; x < ImageSize; x++)
            {
                var pixel = image[x, y];

                tensor[0, 0, y, x] = (pixel.R / 255f - mean[0]) / std[0];
                tensor[0, 1, y, x] = (pixel.G / 255f - mean[1]) / std[1];
                tensor[0, 2, y, x] = (pixel.B / 255f - mean[2]) / std[2];
            }
        }

        return tensor;
    }

    private static float[] Softmax(float[] logits)
    {
        var max = logits.Max();
        var exps = logits.Select(l => MathF.Exp(l - max)).ToArray();
        var sum = exps.Sum();
        return exps.Select(e => e / sum).ToArray();
    }

    public void Dispose() => _session.Dispose();
}