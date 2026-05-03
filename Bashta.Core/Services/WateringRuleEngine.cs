using Bashta.Core.Entities;

namespace Bashta.Core.Services;

public class WateringDecision
{
    public bool ShouldWater { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? SkipReason { get; set; }  // ← dodaj ovo
    public decimal ModifiedThreshold { get; set; }
}

public class WateringRuleEngine
{
    // Baza prag vlage za paradajz iz plant_type tabele
    private const decimal BaseMoistureThreshold = 40m;

    public WateringDecision Evaluate(
        decimal currentMoisture,
        decimal temperature,
        decimal humidity,
        int lux,
        bool rainForecast,
        Disease? activeDisease)
    {
        // 1. Izračunaj modifier na osnovu bolesti
        decimal diseaseModifier = activeDisease?.WateringModifier ?? 1.00m;

        // 2. Prilagodi prag vlage
        decimal adjustedThreshold = BaseMoistureThreshold * diseaseModifier;

        // 3. Lux block — ne zalijevaj tokom jakog sunca (transpiracija je visoka
        //    ali zalijevanje u podne može uzrokovati opekotine i gljivice)
        if (lux > 25000)
        {
            return new WateringDecision
            {
                ShouldWater = false,
                Reason = "lux_block — intenzivno sunce, odgodi zalijevanje",
                ModifiedThreshold = adjustedThreshold
            };
        }

        // 4. Prognoza kiše — preskoči zalijevanje
        if (rainForecast)
        {
            return new WateringDecision
            {
                ShouldWater = false,
                Reason = "rain_forecast — kiša se očekuje u narednih 24h",
                ModifiedThreshold = adjustedThreshold
            };
        }

        // 5. Visoka vlažnost zraka + niska temperatura = manji prioritet
        if (humidity > 80 && temperature < 15)
        {
            adjustedThreshold *= 0.85m;  // smanji prag — biljci treba manje vode
        }

        // 6. Visoka temperatura = veći prioritet zalijevanja
        if (temperature > 28)
        {
            adjustedThreshold *= 1.15m;
        }

        // 7. Konačna odluka
        bool shouldWater = currentMoisture < adjustedThreshold;

        return new WateringDecision
        {
            ShouldWater = shouldWater,
            Reason = shouldWater
                ? $"soil_moisture {currentMoisture}% ispod praga {adjustedThreshold:F1}%"
                : $"soil_moisture_sufficient — {currentMoisture}% iznad praga {adjustedThreshold:F1}%",
            ModifiedThreshold = adjustedThreshold
        };
    }
}