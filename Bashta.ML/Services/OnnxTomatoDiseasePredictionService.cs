using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Bashta.ML.Services;

public class OnnxTomatoDiseasePredictionService
    : ITomatoDiseasePredictionService, IDisposable
{
    private const int ImageSize = 224;

    private static readonly float[] Mean =
    [
        0.485f,
        0.456f,
        0.406f
    ];

    private static readonly float[] Std =
    [
        0.229f,
        0.224f,
        0.225f
    ];

    private readonly InferenceSession _session;
    private readonly string _inputName;

    private readonly string[] _labels =
    [
        "Bacterial spot",
        "Early blight",
        "Late blight",
        "Leaf Mold",
        "Septoria leaf spot",
        "Spider mites",
        "Target Spot",
        "Yellow Leaf Curl Virus",
        "Mosaic virus",
        "Healthy"
    ];

    public OnnxTomatoDiseasePredictionService()
    {
        var modelPath = Path.Combine(
            AppContext.BaseDirectory,
            "Models",
            "bashta_tomato_model.onnx"
        );

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"ONNX model nije pronađen: {modelPath}"
            );
        }

        _session = new InferenceSession(
            modelPath
        );

        _inputName =
            _session.InputMetadata.Keys.First();
    }

    public async Task<TomatoDiseasePredictionResult>
        PredictAsync(
            Stream imageStream,
            CancellationToken cancellationToken = default)
    {
        var inputTensor =
            await PreprocessImageAsync(
                imageStream,
                cancellationToken
            );

        using var results =
            _session.Run(
            [
                NamedOnnxValue.CreateFromTensor(
                    _inputName,
                    inputTensor
                )
            ]);

        var output =
            results
                .First()
                .AsEnumerable<float>()
                .ToArray();

        var probabilities =
            ConvertToProbabilities(
                output
            );

        var bestIndex =
            Array.IndexOf(
                probabilities,
                probabilities.Max()
            );

        var confidence =
            probabilities[bestIndex];

        var label =
            bestIndex >= 0 &&
            bestIndex < _labels.Length
                ? _labels[bestIndex]
                : "Unknown";

        var isHealthy =
            label.Contains(
                "Healthy",
                StringComparison.OrdinalIgnoreCase
            );

        return new TomatoDiseasePredictionResult
        {
            Label = label,

            DiseaseName =
                isHealthy
                    ? "Healthy"
                    : label,

            DiseaseNameLocal =
                MapLocalName(label),

            Confidence =
                confidence,

            IsHealthy =
                isHealthy,

            Recommendation =
                BuildRecommendation(label)
        };
    }

    private static async Task<DenseTensor<float>>
        PreprocessImageAsync(
            Stream imageStream,
            CancellationToken cancellationToken)
    {
        using var image =
            await Image.LoadAsync<Rgb24>(
                imageStream,
                cancellationToken
            );

        /*
         * Python:
         *
         * transforms.Resize((224, 224))
         *
         * Zato ovdje ne koristimo Crop.
         * Slika se prilagođava direktno na
         * 224 x 224 piksela.
         */
        image.Mutate(
            operation =>
                operation.Resize(
                    new ResizeOptions
                    {
                        Size =
                            new Size(
                                ImageSize,
                                ImageSize
                            ),

                        Mode =
                            ResizeMode.Stretch,

                        /*
                         * Triangle filter je približan
                         * bilinearnoj interpolaciji
                         * koju torchvision koristi
                         * za Resize.
                         */
                        Sampler =
                            KnownResamplers.Triangle
                    }
                )
        );

        var tensor =
            new DenseTensor<float>(
                new[]
                {
                    1,
                    3,
                    ImageSize,
                    ImageSize
                }
            );

        for (
            var y = 0;
            y < ImageSize;
            y++
        )
        {
            for (
                var x = 0;
                x < ImageSize;
                x++
            )
            {
                var pixel =
                    image[x, y];

                /*
                 * Isto kao transforms.ToTensor():
                 * uint8 RGB -> [0, 1]
                 */
                var red =
                    pixel.R / 255f;

                var green =
                    pixel.G / 255f;

                var blue =
                    pixel.B / 255f;

                /*
                 * Isto kao Python:
                 *
                 * Normalize(
                 *   mean=[0.485,0.456,0.406],
                 *   std =[0.229,0.224,0.225]
                 * )
                 */

                tensor[0, 0, y, x] =
                    (red - Mean[0]) /
                    Std[0];

                tensor[0, 1, y, x] =
                    (green - Mean[1]) /
                    Std[1];

                tensor[0, 2, y, x] =
                    (blue - Mean[2]) /
                    Std[2];
            }
        }

        return tensor;
    }

    private static float[] ConvertToProbabilities(
        float[] output)
    {
        var looksLikeProbabilities =
            output.All(
                value =>
                    value >= 0f &&
                    value <= 1f
            )
            &&
            Math.Abs(
                output.Sum() - 1f
            ) < 0.05f;

        if (looksLikeProbabilities)
        {
            return output;
        }

        var max =
            output.Max();

        var exp =
            output
                .Select(
                    value =>
                        MathF.Exp(
                            value - max
                        )
                )
                .ToArray();

        var sum =
            exp.Sum();

        return exp
            .Select(
                value =>
                    value / sum
            )
            .ToArray();
    }

    private static string? MapLocalName(
        string label)
    {
        return label switch
        {
            "Bacterial spot" =>
                "Bakterijska pjegavost",

            "Early blight" =>
                "Rana plamenjača",

            "Late blight" =>
                "Kasna plamenjača",

            "Leaf Mold" =>
                "Lisna plijesan",

            "Septoria leaf spot" =>
                "Septorijska pjegavost lista",

            "Spider mites" =>
                "Paukove grinje",

            "Target Spot" =>
                "Target pjegavost",

            "Yellow Leaf Curl Virus" =>
                "Virus žutog kovrčanja lista",

            "Mosaic virus" =>
                "Mozaik virus paradajza",

            "Healthy" =>
                "Zdrava biljka",

            _ =>
                null
        };
    }

    private static string BuildRecommendation(
        string label)
    {
        return label switch
        {
            "Healthy" =>
                "Biljka izgleda zdravo. Nastaviti redovno praćenje vlažnosti tla, temperature i osvjetljenja.",

            "Early blight" =>
                "Detektovani su simptomi rane plamenjače. Preporučuje se uklanjanje zaraženih listova, izbjegavanje kvašenja lista i bolja cirkulacija zraka.",

            "Late blight" =>
                "Detektovani su simptomi kasne plamenjače. Potrebno je brzo ukloniti zahvaćene dijelove biljke i smanjiti vlagu na listovima.",

            "Bacterial spot" =>
                "Detektovana je bakterijska pjegavost. Preporučuje se uklanjanje zaraženih listova i izbjegavanje prskanja vode po listovima.",

            "Leaf Mold" =>
                "Detektovana je lisna plijesan. Preporučuje se bolja ventilacija i smanjenje vlažnosti oko biljke.",

            "Septoria leaf spot" =>
                "Detektovana je septorijska pjegavost lista. Preporučuje se uklanjanje donjih zaraženih listova i održavanje listova suhim.",

            "Spider mites" =>
                "Detektovani su simptomi napada paukovih grinja. Preporučuje se pregled naličja listova i uklanjanje jako zahvaćenih dijelova.",

            "Target Spot" =>
                "Detektovana je target pjegavost. Preporučuje se uklanjanje zaraženih listova i praćenje širenja simptoma.",

            "Yellow Leaf Curl Virus" =>
                "Detektovani su simptomi virusa žutog kovrčanja lista. Preporučuje se izolacija biljke i kontrola insekata prenosilaca.",

            "Mosaic virus" =>
                "Detektovani su simptomi mozaik virusa. Preporučuje se izolacija biljke i dezinfekcija alata nakon kontakta.",

            _ =>
                "Model je vratio nepoznatu klasu. Preporučuje se ručna provjera slike i ponovna analiza."
        };
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}