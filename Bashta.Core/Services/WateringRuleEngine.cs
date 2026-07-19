namespace Bashta.Core.Services;

public class WateringRuleInput
{
    public int? PlantId { get; set; }

    public decimal? CurrentSoilMoisture { get; set; }

    public int? MinRecommendedSoilMoisture { get; set; }

    public int? MaxRecommendedSoilMoisture { get; set; }

    public decimal? CurrentTemperature { get; set; }

    public decimal? MaxTemperatureNext24h { get; set; }

    public int? CurrentLux { get; set; }

    public bool IsRainExposed { get; set; }

    public bool WeatherAvailable { get; set; }

    public bool RainExpectedIn24h { get; set; }

    public decimal RainAmountNext24hMm { get; set; }

    public string RainIntensity { get; set; } = "none";

    public bool HeatRiskNext24h { get; set; }

    public int WateringCountLast24h { get; set; }

    public int MaxWateringCountLast24h { get; set; }

    public DateTime LocalNow { get; set; } = DateTime.Now;
}

public class WateringDecision
{
    public bool CanWater { get; set; }

    public bool IsWateringRecommended { get; set; }

    public bool RequiresForce { get; set; }

    public bool IsAutomaticWateringAllowedNow { get; set; }

    public int RecommendedAmountMl { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    public string? WarningMessage { get; set; }

    public string? WeatherImpactMessage { get; set; }

    public string NextRecommendedWateringWindow { get; set; } = string.Empty;

    public string DecisionReason { get; set; } = string.Empty;

    //Kompatabilnost sa starim controllerom
    public bool ShouldWater => IsWateringRecommended;

    public string Reason => StatusMessage;

    public string? SkipReason => IsWateringRecommended ? null : StatusMessage;
}

public class WateringRuleEngine
{
    private const int LowAmountMl = 100;
    private const int MediumAmountMl = 150;
    private const int HighAmountMl = 200;
    private const int HeatAdjustmentMl = 50;
    private const int MaxRecommendedAmountMl = 250;

    private const int HighSunLuxThreshold = 10000;
    //kompatabilnost sa starim kontr
    public WateringDecision Evaluate(
    decimal? currentMoisture,
    int? minMoisture,
    int? maxMoisture,
    decimal? temperature = null,
    decimal? humidity = null,
    int? lux = null,
    bool rainForecast = false)
    {
        return Evaluate(new WateringRuleInput
        {
            PlantId = 0,
            CurrentSoilMoisture = currentMoisture,
            MinRecommendedSoilMoisture = minMoisture,
            MaxRecommendedSoilMoisture = maxMoisture,
            CurrentTemperature = temperature,
            MaxTemperatureNext24h = temperature,
            CurrentLux = lux,
            WeatherAvailable = true,
            RainExpectedIn24h = rainForecast,
            RainAmountNext24hMm = rainForecast ? 1m : 0m,
            RainIntensity = rainForecast ? "light" : "none",
            IsRainExposed = false,
            HeatRiskNext24h = temperature is >= 30m,
            WateringCountLast24h = 0,
            MaxWateringCountLast24h = 2,
            LocalNow = DateTime.Now
        });
    }
    //kraj dodatog dijela
    public WateringDecision Evaluate(WateringRuleInput input)
    {
        var decision = new WateringDecision
        {
            CanWater = true,
            IsWateringRecommended = false,
            RequiresForce = false,
            IsAutomaticWateringAllowedNow = false,
            RecommendedAmountMl = 0,
            NextRecommendedWateringWindow = GetNextRecommendedWindow(input.LocalNow)
        };

        if (input.PlantId is null)
        {
            decision.CanWater = false;
            decision.StatusMessage = "Saksija nema aktivnu biljku.";
            decision.DecisionReason = "no_active_plant";
            return decision;
        }

        if (input.WateringCountLast24h >= input.MaxWateringCountLast24h)
        {
            decision.CanWater = false;
            decision.StatusMessage =
                "Zalijevanje nije dostupno jer je dostignut limit zalijevanja u posljednja 24 sata.";
            decision.DecisionReason = "daily_limit_reached";
            return decision;
        }

        var isPreferredTime = IsPreferredWateringTime(input.LocalNow);
        var isStrongSun = IsStrongSun(input.LocalNow, input.CurrentLux);
        if (input.CurrentSoilMoisture is null)
        {
            decision.CanWater = true;
            decision.IsWateringRecommended = false;
            decision.RequiresForce = true;
            decision.IsAutomaticWateringAllowedNow = false;
            decision.RecommendedAmountMl = LowAmountMl;
            decision.StatusMessage =
                "Nema dostupnog očitanja vlažnosti tla. Automatsko zalijevanje nije preporučeno dok se ne dobije novo senzorsko očitanje.";
            decision.WarningMessage =
                "Ručno zalijevanje je moguće samo uz potvrdu korisnika, jer sistem nema aktuelan podatak o vlažnosti tla.";
            decision.WeatherImpactMessage = input.WeatherAvailable
                ? "Vremenska prognoza je dostupna, ali odluka o zalijevanju se ne može pouzdano donijeti bez očitanja vlažnosti tla."
                : "Vremenska prognoza nije dostupna, a nema ni očitanja vlažnosti tla.";
            decision.NextRecommendedWateringWindow = GetNextRecommendedWindow(input.LocalNow);
            decision.DecisionReason =
                $"soil=n/a; sensorMissing=True; rainExposed={input.IsRainExposed}; " +
                $"rain24h={input.RainAmountNext24hMm:0.#}mm; " +
                $"maxTemp24h={input.MaxTemperatureNext24h?.ToString("0.#") ?? "n/a"}°C; " +
                $"recommended={decision.RecommendedAmountMl}ml; " +
                $"autoAllowedNow=False; requiresForce=True";

            return decision;
        }
        var amount = CalculateBaseAmount(input);

        ApplyHeatAdjustment(input, ref amount);
        ApplyRainAdjustment(input, ref amount, decision);

        decision.RecommendedAmountMl = Math.Clamp(amount, 0, MaxRecommendedAmountMl);
        decision.IsWateringRecommended = decision.RecommendedAmountMl > 0;

        ApplyStatusMessage(input, decision);

        if (isStrongSun)
        {
            decision.WarningMessage =
                "Trenutno nije idealno vrijeme za zalijevanje zbog jakog sunca. Preporučuje se zalijevanje ujutro ili navečer.";

            if (decision.IsWateringRecommended)
                decision.RequiresForce = true;
        }

        decision.IsAutomaticWateringAllowedNow =
            decision.IsWateringRecommended &&
            isPreferredTime &&
            !isStrongSun &&
            !decision.RequiresForce;

        if (!decision.IsWateringRecommended && decision.CanWater)
        {
            decision.RequiresForce = true;
        }

        decision.DecisionReason = BuildDecisionReason(input, decision);

        return decision;
    }

    private static int CalculateBaseAmount(WateringRuleInput input)
    {
        if (input.CurrentSoilMoisture is null)
            return LowAmountMl;

        if (input.MinRecommendedSoilMoisture is null)
            return LowAmountMl;

        var deficit = input.MinRecommendedSoilMoisture.Value - input.CurrentSoilMoisture.Value;

        if (deficit <= 0)
            return 0;

        if (deficit <= 5)
            return LowAmountMl;

        if (deficit <= 15)
            return MediumAmountMl;

        return HighAmountMl;
    }

    private static void ApplyHeatAdjustment(WateringRuleInput input, ref int amount)
    {
        var currentTempHigh = input.CurrentTemperature is >= 30m;
        var forecastTempHigh = input.MaxTemperatureNext24h is >= 32m;

        if (!currentTempHigh && !forecastTempHigh && !input.HeatRiskNext24h)
            return;

        if (amount <= 0)
            return;

        amount += HeatAdjustmentMl;
    }

    private static void ApplyRainAdjustment(
        WateringRuleInput input,
        ref int amount,
        WateringDecision decision)
    {
        if (!input.WeatherAvailable)
        {
            decision.WeatherImpactMessage =
                "Vremenska prognoza nije dostupna, odluka se zasniva na senzorskim podacima.";
            return;
        }

        if (!input.RainExpectedIn24h || input.RainAmountNext24hMm <= 0)
        {
            decision.WeatherImpactMessage =
                "Kiša nije očekivana u naredna 24 sata.";
            return;
        }

        if (!input.IsRainExposed)
        {
            decision.WeatherImpactMessage =
                "Kiša je očekivana, ali saksija nije označena kao izložena kiši. Kiša ne utiče na količinu zalijevanja.";
            return;
        }

        if (input.RainAmountNext24hMm >= 7)
        {
            amount = 0;
            decision.RequiresForce = true;
            decision.WeatherImpactMessage =
                "Očekuje se značajnija kiša. Automatsko zalijevanje se odgađa jer je saksija izložena kiši.";
            return;
        }

        if (input.RainAmountNext24hMm >= 3)
        {
            amount = Math.Max(0, amount - 100);
            decision.WeatherImpactMessage =
                "Očekuje se umjerena kiša. Preporučena količina vode je smanjena.";
            return;
        }

        amount = Math.Max(0, amount - 50);
        decision.WeatherImpactMessage =
            "Očekuje se manja količina kiše. Preporučena količina vode je blago smanjena.";
    }

    private static void ApplyStatusMessage(
        WateringRuleInput input,
        WateringDecision decision)
    {
        if (input.CurrentSoilMoisture is null)
        {
            decision.StatusMessage =
                "Nema dostupnog očitanja vlažnosti tla. Zalijevanje je moguće, ali se preporučuje provjera senzora.";
            return;
        }

        if (input.MinRecommendedSoilMoisture is null ||
            input.MaxRecommendedSoilMoisture is null)
        {
            decision.StatusMessage =
                "Nema definisanog preporučenog opsega vlage za ovu biljku.";
            return;
        }

        if (input.CurrentSoilMoisture < input.MinRecommendedSoilMoisture)
        {
            decision.StatusMessage =
                $"Vlažnost tla je ispod preporučenog opsega. Preporučena količina: {decision.RecommendedAmountMl} ml.";
            return;
        }

        if (input.CurrentSoilMoisture > input.MaxRecommendedSoilMoisture)
        {
            decision.StatusMessage =
                "Vlažnost tla je iznad preporučenog opsega. Zalijevanje trenutno nije preporučeno.";
            return;
        }

        decision.StatusMessage =
            "Vlažnost tla je u preporučenom opsegu. Zalijevanje trenutno nije potrebno.";
    }

    private static bool IsPreferredWateringTime(DateTime localNow)
    {
        var hour = localNow.Hour;

        return hour is >= 5 and <= 8 or >= 19 and <= 22;
    }

    private static bool IsStrongSun(DateTime localNow, int? currentLux)
    {
        var isStrongSunPeriod = localNow.Hour is >= 10 and <= 17;
        var isHighLux = currentLux is >= HighSunLuxThreshold;

        return isStrongSunPeriod || isHighLux;
    }

    private static string GetNextRecommendedWindow(DateTime localNow)
    {
        var hour = localNow.Hour;

        if (hour < 5)
            return "Naredni preporučeni termin: jutro, 05:00–08:00.";

        if (hour <= 8)
            return "Trenutno je preporučeni jutarnji termin za zalijevanje.";

        if (hour < 19)
            return "Naredni preporučeni termin: večer, 19:00–22:00.";

        if (hour <= 22)
            return "Trenutno je preporučeni večernji termin za zalijevanje.";

        return "Naredni preporučeni termin: sutra ujutro, 05:00–08:00.";
    }

    private static string BuildDecisionReason(
        WateringRuleInput input,
        WateringDecision decision)
    {
        return
            $"soil={input.CurrentSoilMoisture?.ToString("0.#") ?? "n/a"}%; " +
            $"min={input.MinRecommendedSoilMoisture?.ToString() ?? "n/a"}%; " +
            $"rainExposed={input.IsRainExposed}; " +
            $"rain24h={input.RainAmountNext24hMm:0.#}mm; " +
            $"maxTemp24h={input.MaxTemperatureNext24h?.ToString("0.#") ?? "n/a"}°C; " +
            $"recommended={decision.RecommendedAmountMl}ml; " +
            $"autoAllowedNow={decision.IsAutomaticWateringAllowedNow}; " +
            $"requiresForce={decision.RequiresForce}";
    }
}