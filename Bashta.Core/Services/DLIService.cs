namespace Bashta.Core.Services;

public class DLIService
{
    // DLI = suma (lux × 0.0185) × interval u satima
    // Konverzija: 1 lux ≈ 0.0185 µmol/m²/s za sunčevu svjetlost
    private const double LuxToMmol = 0.0185;

    public double CalculateDLI(IEnumerable<(DateTime Time, int Lux)> readings)
    {
        double dli = 0;
        var list = readings.OrderBy(r => r.Time).ToList();

        for (int i = 1; i < list.Count; i++)
        {
            var intervalHours = (list[i].Time - list[i - 1].Time).TotalHours;
            var avgLux = (list[i].Lux + list[i - 1].Lux) / 2.0;
            dli += avgLux * LuxToMmol * intervalHours * 3.6; // µmol → mol
        }

        return Math.Round(dli, 2);
    }

    public string EvaluateDLI(double actualDli, double targetDli)
    {
        var ratio = actualDli / targetDli;

        return ratio switch
        {
            < 0.5 => "Kritično niska svjetlost. Premjesti biljku ili dodaj grow lampu.",
            < 0.75 => "Nedovoljna svjetlost. Razmisli o premještaju na sunčanije mjesto.",
            < 0.90 => "Svjetlost je blizu optimalne ali može biti bolja.",
            <= 1.1 => "Svjetlost je optimalna.",
            _ => "Prekomjerna izloženost svjetlosti. Razmisli o sjenčenju."
        };
    }
}