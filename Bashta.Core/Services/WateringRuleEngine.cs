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

    // Weather context
    public bool IsRainExposed { get; set; }

    public bool WeatherAvailable { get; set; }

    public bool RainExpectedIn24h { get; set; }

    public decimal RainAmountNext24hMm { get; set; }

    public bool RainExpectedBeforeNextWindow { get; set; }

    public decimal RainAmountBeforeNextWindowMm { get; set; }

    public string RainIntensity { get; set; } = "none";

    public bool HeatRiskNext24h { get; set; }

    /*
     * Ostavljeno radi kompatibilnosti sa starijim
     * controllerima/DTO-ovima.
     *
     * Više se NE koristi kao blokada zalijevanja.
     */
    public int WateringCountLast24h { get; set; }

    public int MaxWateringCountLast24h { get; set; }

    /*
     * Konfiguracijski koeficijent zdravstvenog stanja.
     *
     * 1.00 = bez korekcije
     * <1.00 = smanjenje količine
     * >1.00 = povećanje količine
     *
     * Engine ga sigurnosno ograničava
     * na raspon 0.90–1.10.
     */
    public decimal DiseaseWateringModifier { get; set; } = 1.00m;

    public string? ActiveDiseaseName { get; set; }

    public DateTime LocalNow { get; set; } = DateTime.Now;
}

public class WateringDecision
{
    public bool CanWater { get; set; }

    public bool IsWateringRecommended { get; set; }

    public bool RequiresForce { get; set; }

    public bool IsAutomaticWateringAllowedNow { get; set; }

    public int RecommendedAmountMl { get; set; }

    public int BaseAmountMl { get; set; }

    public int CriticalMoistureThreshold { get; set; }

    public decimal MoistureDeficit { get; set; }

    public bool IsCriticalMoisture { get; set; }

    public bool WaitingForRain { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    public string? WarningMessage { get; set; }

    public string? WeatherImpactMessage { get; set; }

    public string? DiseaseImpactMessage { get; set; }

    public string NextRecommendedWateringWindow { get; set; } =
        string.Empty;

    public string DecisionReason { get; set; } = string.Empty;

    // Kompatibilnost sa starijim kodom
    public bool ShouldWater => IsWateringRecommended;

    public string Reason => StatusMessage;

    public string? SkipReason =>
        IsWateringRecommended
            ? null
            : StatusMessage;
}

public class WateringRuleEngine
{
    /*
     * Konfiguracija PROTOTIPA.
     *
     * Ovo nisu univerzalne agronomske vrijednosti.
     * Parametri predstavljaju konfiguraciju
     * prototipske decision-support logike.
     */

    private const int MinimumDoseMl = 50;

    private const int MaximumDoseMl = 500;

    private const int MlPerDeficitPoint = 10;

    /*
     * Kritična vlaga:
     * MinRecommended - 15 procentnih poena.
     *
     * Npr. minimum 70 %:
     * critical = 55 %.
     */
    private const int CriticalDeficitPoints = 15;

    /*
     * Ako je jako toplo, količina može
     * biti povećana za 10 %.
     */
    private const decimal HeatMultiplier = 1.10m;

    private const int HighSunLuxThreshold = 10000;

    // Kompatibilnost sa starim pozivima
    public WateringDecision Evaluate(
        decimal? currentMoisture,
        int? minMoisture,
        int? maxMoisture,
        decimal? temperature = null,
        decimal? humidity = null,
        int? lux = null,
        bool rainForecast = false)
    {
        return Evaluate(
            new WateringRuleInput
            {
                PlantId = 0,

                CurrentSoilMoisture =
                    currentMoisture,

                MinRecommendedSoilMoisture =
                    minMoisture,

                MaxRecommendedSoilMoisture =
                    maxMoisture,

                CurrentTemperature =
                    temperature,

                MaxTemperatureNext24h =
                    temperature,

                CurrentLux =
                    lux,

                WeatherAvailable = true,

                RainExpectedIn24h =
                    rainForecast,

                RainAmountNext24hMm =
                    rainForecast
                        ? 1m
                        : 0m,

                RainExpectedBeforeNextWindow =
                    false,

                RainAmountBeforeNextWindowMm =
                    0m,

                RainIntensity =
                    rainForecast
                        ? "light"
                        : "none",

                IsRainExposed = false,

                HeatRiskNext24h =
                    temperature is >= 30m,

                DiseaseWateringModifier =
                    1.00m,

                LocalNow =
                    DateTime.Now
            });
    }

    public WateringDecision Evaluate(
        WateringRuleInput input)
    {
        var decision =
            new WateringDecision
            {
                CanWater = true,

                IsWateringRecommended = false,

                RequiresForce = false,

                IsAutomaticWateringAllowedNow = false,

                RecommendedAmountMl = 0,

                BaseAmountMl = 0,

                NextRecommendedWateringWindow =
                    GetNextRecommendedWindow(
                        input.LocalNow)
            };

        // 1. Aktivna biljka
        if (input.PlantId is null)
        {
            decision.CanWater = false;

            decision.StatusMessage =
                "Saksija nema aktivnu biljku.";

            decision.DecisionReason =
                "no_active_plant";

            return decision;
        }

        /*
         * Više nema blokade tipa:
         *
         * WateringCountLast24h >= 2
         *
         * Novo senzorsko stanje je
         * primarni kriterij odluke.
         */

        // 2. Nema očitanja vlažnosti
        if (input.CurrentSoilMoisture is null)
        {
            decision.CanWater = true;

            decision.IsWateringRecommended = false;

            decision.RequiresForce = true;

            decision.IsAutomaticWateringAllowedNow = false;

            decision.RecommendedAmountMl =
                MinimumDoseMl;

            decision.StatusMessage =
                "Nema dostupnog očitanja vlažnosti tla. " +
                "Automatsko zalijevanje nije moguće pouzdano procijeniti.";

            decision.WarningMessage =
                "Ručno zalijevanje je moguće uz potvrdu korisnika.";

            decision.WeatherImpactMessage =
                input.WeatherAvailable
                    ? "Vremenska prognoza je dostupna, ali bez očitanja vlažnosti tla nije dovoljna za automatsku odluku."
                    : "Vremenska prognoza i očitanje vlažnosti tla nisu dostupni.";

            decision.DecisionReason =
                "soil=n/a; autoAllowedNow=False; requiresForce=True";

            return decision;
        }

        // 3. Nema definisanog minimalnog praga
        if (input.MinRecommendedSoilMoisture is null)
        {
            decision.CanWater = true;

            decision.RequiresForce = true;

            decision.StatusMessage =
                "Nije definisan minimalni preporučeni nivo vlažnosti za ovu biljku.";

            decision.DecisionReason =
                "min_soil_moisture_missing";

            return decision;
        }

        var currentMoisture =
            input.CurrentSoilMoisture.Value;

        var minMoisture =
            input.MinRecommendedSoilMoisture.Value;

        // 4. Kritični prag
        var criticalThreshold =
            Math.Max(
                0,
                minMoisture -
                CriticalDeficitPoints);

        decision.CriticalMoistureThreshold =
            criticalThreshold;

        decision.IsCriticalMoisture =
            currentMoisture <=
            criticalThreshold;

        // 5. Vlažnost je već dovoljna
        if (currentMoisture >= minMoisture)
        {
            decision.CanWater = true;

            decision.IsWateringRecommended = false;

            decision.RequiresForce = true;

            decision.RecommendedAmountMl = 0;

            if (input.MaxRecommendedSoilMoisture.HasValue &&
                currentMoisture >
                input.MaxRecommendedSoilMoisture.Value)
            {
                decision.StatusMessage =
                    "Vlažnost tla je iznad preporučenog opsega. " +
                    "Zalijevanje trenutno nije preporučeno.";
            }
            else
            {
                decision.StatusMessage =
                    "Vlažnost tla je u preporučenom opsegu. " +
                    "Zalijevanje trenutno nije potrebno.";
            }

            decision.WeatherImpactMessage =
                BuildNoWaterWeatherMessage(
                    input);

            decision.DiseaseImpactMessage =
                BuildNoWaterDiseaseMessage(
                    input);

            decision.DecisionReason =
                BuildDecisionReason(
                    input,
                    decision);

            return decision;
        }

        // 6. Deficit vlage
        var deficit =
            minMoisture -
            currentMoisture;

        decision.MoistureDeficit =
            deficit;

        /*
         * 7. Osnovna količina:
         *
         * Q = 50 + deficit × 10
         */
        var baseAmount =
            MinimumDoseMl +
            (int)Math.Round(
                deficit *
                MlPerDeficitPoint);

        baseAmount =
            Math.Clamp(
                baseAmount,
                MinimumDoseMl,
                MaximumDoseMl);

        decision.BaseAmountMl =
            baseAmount;

        decimal adjustedAmount =
            baseAmount;

        // 8. Korekcija zbog toplote
        if (IsHeatRisk(input))
        {
            adjustedAmount *=
                HeatMultiplier;
        }

        // 9. Korekcija zbog prognoze
        ApplyRainLogic(
            input,
            decision,
            ref adjustedAmount);

        /*
         * Ako kiša opravdava potpuno čekanje,
         * završavamo odluku ovdje.
         */
        if (decision.WaitingForRain)
        {
            decision.RecommendedAmountMl = 0;

            decision.IsWateringRecommended = false;

            decision.RequiresForce = true;

            decision.IsAutomaticWateringAllowedNow = false;

            decision.StatusMessage =
                "Vlažnost tla je ispod preporučenog nivoa, " +
                "ali nije u kritičnoj zoni. " +
                "Automatsko zalijevanje je privremeno odgođeno " +
                "zbog očekivanih padavina.";

            decision.DiseaseImpactMessage =
                BuildNoWaterDiseaseMessage(
                    input);

            decision.DecisionReason =
                BuildDecisionReason(
                    input,
                    decision);

            return decision;
        }

        // 10. Korekcija zbog zdravstvenog stanja
        ApplyDiseaseAdjustment(
            input,
            decision,
            ref adjustedAmount);

        // 11. Konačni sigurnosni raspon
        var finalAmount =
            (int)Math.Round(
                adjustedAmount);

        finalAmount =
            Math.Clamp(
                finalAmount,
                MinimumDoseMl,
                MaximumDoseMl);

        decision.RecommendedAmountMl =
            finalAmount;

        decision.IsWateringRecommended =
            true;

        decision.StatusMessage =
            decision.IsCriticalMoisture
                ? $"Vlažnost tla je kritično niska. Zalijevanje je preporučeno u količini od {finalAmount} ml."
                : $"Vlažnost tla je ispod preporučenog opsega. Preporučena količina je {finalAmount} ml.";

        // 12. Termin i jako sunce
        var isPreferredTime =
            IsPreferredWateringTime(
                input.LocalNow);

        var isStrongSun =
            IsStrongSun(
                input.LocalNow,
                input.CurrentLux);
        if (decision.IsWateringRecommended &&
    !decision.IsCriticalMoisture &&
    !isPreferredTime)
        {
            decision.WarningMessage =
                "Zalijevanje je preporučeno, ali automatska intervencija je odgođena do narednog preporučenog termina.";
        }

        if (isStrongSun)
        {
            decision.WarningMessage =
                "Trenutno nije idealno vrijeme za zalijevanje zbog jakog sunca. " +
                "Preporučuje se zalijevanje ujutro ili navečer.";

            /*
             * Kod nekritične vlage sistem čeka
             * pogodniji termin.
             */
            if (!decision.IsCriticalMoisture)
            {
                decision.RequiresForce =
                    true;
            }
        }

        /*
         * Kritično stanje može nadjačati
         * preferred-time ograničenje.
         */
        if (decision.IsCriticalMoisture &&
            !isPreferredTime)
        {
            decision.WarningMessage =
                isStrongSun
                    ? "Vlažnost tla je u kritičnoj zoni. " +
                      "Automatsko zalijevanje je dozvoljeno i izvan preporučenog termina, " +
                      "iako trenutno postoje nepovoljni uslovi jakog sunca."
                    : "Vlažnost tla je u kritičnoj zoni. " +
                      "Automatsko zalijevanje je dozvoljeno i izvan preporučenog termina.";
        }

        decision.IsAutomaticWateringAllowedNow =
            decision.IsWateringRecommended &&
            (
                decision.IsCriticalMoisture ||
                isPreferredTime
            ) &&
            (
                decision.IsCriticalMoisture ||
                !isStrongSun
            ) &&
            !decision.RequiresForce;

        decision.DecisionReason =
            BuildDecisionReason(
                input,
                decision);

        return decision;
    }

    private static void ApplyRainLogic(
        WateringRuleInput input,
        WateringDecision decision,
        ref decimal amount)
    {
        if (!input.WeatherAvailable)
        {
            decision.WeatherImpactMessage =
                "Vremenska prognoza nije dostupna. " +
                "Odluka se zasniva prvenstveno na senzorskom očitanju.";

            return;
        }

        if (!input.IsRainExposed)
        {
            decision.WeatherImpactMessage =
                input.RainExpectedIn24h
                    ? "Kiša je očekivana, ali saksija nije izložena padavinama. " +
                      "Prognoza ne mijenja količinu zalijevanja."
                    : "Kiša nije očekivana. " +
                      "Vremenski kontekst ne zahtijeva korekciju količine.";

            return;
        }

        /*
         * Za odluku je relevantna kiša prije
         * narednog preporučenog termina.
         */
        if (!input.RainExpectedBeforeNextWindow ||
            input.RainAmountBeforeNextWindowMm <= 0)
        {
            decision.WeatherImpactMessage =
                input.RainExpectedIn24h
                    ? "Padavine se očekuju u naredna 24 sata, " +
                      "ali ne prije narednog preporučenog termina zalijevanja."
                    : "Kiša nije očekivana prije narednog preporučenog termina zalijevanja.";

            return;
        }

        var rain =
            input.RainAmountBeforeNextWindowMm;

        // < 1 mm: zanemariv uticaj
        if (rain < 1m)
        {
            decision.WeatherImpactMessage =
                $"Do narednog termina očekuje se samo {rain:0.#} mm padavina. " +
                "Količina je premala da bi uticala na odluku zalijevanja.";

            return;
        }

        // 1–3 mm: blaga korekcija
        if (rain < 3m)
        {
            amount *=
                decision.IsCriticalMoisture
                    ? 0.90m
                    : 0.85m;

            decision.WeatherImpactMessage =
                $"Do narednog termina očekuje se {rain:0.#} mm padavina. " +
                "Prognoza je uzeta u obzir kroz blago smanjenje količine vode.";

            return;
        }

        /*
         * >= 3 mm, a vlaga nije kritična:
         * moguće je čekati kišu.
         */
        if (!decision.IsCriticalMoisture)
        {
            decision.WaitingForRain =
                true;

            decision.WeatherImpactMessage =
                $"Do narednog preporučenog termina očekuje se {rain:0.#} mm padavina. " +
                "Vlažnost tla je ispod optimuma, ali nije kritično niska, " +
                "pa se automatsko zalijevanje odgađa.";

            return;
        }

        /*
         * Kritično suho tlo:
         * intervencija se ne odgađa potpuno.
         */

        // 3–7 mm
        if (rain < 7m)
        {
            amount *= 0.70m;

            decision.WeatherImpactMessage =
                $"Do narednog termina očekuje se {rain:0.#} mm padavina, " +
                "ali je vlažnost tla kritično niska. " +
                "Zalijevanje se izvršava smanjenom količinom " +
                "umjesto potpunog odgađanja.";

            return;
        }

        // >= 7 mm
        amount *= 0.60m;

        decision.WeatherImpactMessage =
            $"Do narednog termina očekuje se {rain:0.#} mm obilnijih padavina, " +
            "ali je vlažnost tla kritično niska. " +
            "Primjenjuje se ograničena interventna količina vode " +
            "do očekivanih padavina.";
    }

    private static void ApplyDiseaseAdjustment(
        WateringRuleInput input,
        WateringDecision decision,
        ref decimal amount)
    {
        var modifier =
            input.DiseaseWateringModifier <= 0
                ? 1.00m
                : input.DiseaseWateringModifier;

        /*
         * Sigurnosna granica prototipa:
         * maksimalna korekcija ±10 %.
         */
        modifier =
            Math.Clamp(
                modifier,
                0.90m,
                1.10m);

        if (modifier == 1.00m)
        {
            decision.DiseaseImpactMessage =
                string.IsNullOrWhiteSpace(
                    input.ActiveDiseaseName)
                    ? "Nema aktivne korekcije zalijevanja povezane sa zdravstvenim stanjem biljke."
                    : $"Detektovano stanje \"{input.ActiveDiseaseName}\" " +
                      "ne mijenja preporučenu količinu zalijevanja.";

            return;
        }

        var amountBeforeDiseaseAdjustment =
            amount;

        amount *=
            modifier;

        var changePercent =
            (modifier - 1.00m) *
            100m;

        if (changePercent < 0)
        {
            decision.DiseaseImpactMessage =
                $"Detektovano stanje \"{input.ActiveDiseaseName}\" " +
                $"primjenjuje koeficijent {modifier:0.00}. " +
                $"Preporučena količina je smanjena za " +
                $"{Math.Abs(changePercent):0}% " +
                $"({amountBeforeDiseaseAdjustment:0} ml → {amount:0} ml).";
        }
        else
        {
            decision.DiseaseImpactMessage =
                $"Detektovano stanje \"{input.ActiveDiseaseName}\" " +
                $"primjenjuje koeficijent {modifier:0.00}. " +
                $"Preporučena količina je povećana za " +
                $"{changePercent:0}% " +
                $"({amountBeforeDiseaseAdjustment:0} ml → {amount:0} ml).";
        }
    }

    private static bool IsHeatRisk(
        WateringRuleInput input)
    {
        return
            input.CurrentTemperature is >= 30m ||
            input.MaxTemperatureNext24h is >= 32m ||
            input.HeatRiskNext24h;
    }

    private static string BuildNoWaterWeatherMessage(
        WateringRuleInput input)
    {
        if (!input.WeatherAvailable)
        {
            return
                "Vremenska prognoza nije dostupna, ali je trenutno senzorsko očitanje vlage dovoljno za zaključak da zalijevanje nije potrebno.";
        }

        if (input.RainExpectedIn24h &&
            input.IsRainExposed)
        {
            return
                "Očekuju se padavine, a trenutna vlažnost tla je već zadovoljavajuća. " +
                "Zalijevanje nije potrebno.";
        }

        return
            "Vremenski uslovi trenutno ne zahtijevaju dodatnu korekciju odluke.";
    }

    private static string BuildNoWaterDiseaseMessage(
        WateringRuleInput input)
    {
        if (string.IsNullOrWhiteSpace(
            input.ActiveDiseaseName))
        {
            return
                "Nema aktivnog zdravstvenog stanja koje utiče na odluku zalijevanja.";
        }

        return
            $"Detektovano stanje \"{input.ActiveDiseaseName}\" " +
            "nije primijenilo korekciju količine jer zalijevanje trenutno nije preporučeno.";
    }

    private static bool IsPreferredWateringTime(
        DateTime localNow)
    {
        var time =
            localNow.TimeOfDay;

        var morningStart =
            new TimeSpan(5, 0, 0);

        var morningEnd =
            new TimeSpan(8, 0, 0);

        var eveningStart =
            new TimeSpan(19, 0, 0);

        var eveningEnd =
            new TimeSpan(22, 0, 0);

        return
            (
                time >= morningStart &&
                time <= morningEnd
            )
            ||
            (
                time >= eveningStart &&
                time <= eveningEnd
            );
    }

    private static bool IsStrongSun(
        DateTime localNow,
        int? currentLux)
    {
        var isStrongSunPeriod =
            localNow.Hour is >= 10 and <= 17;

        var isHighLux =
            currentLux is >=
            HighSunLuxThreshold;

        return
            isStrongSunPeriod ||
            isHighLux;
    }

    private static string GetNextRecommendedWindow(
        DateTime localNow)
    {
        var time =
            localNow.TimeOfDay;

        var morningStart =
            new TimeSpan(5, 0, 0);

        var morningEnd =
            new TimeSpan(8, 0, 0);

        var eveningStart =
            new TimeSpan(19, 0, 0);

        var eveningEnd =
            new TimeSpan(22, 0, 0);

        if (time < morningStart)
        {
            return
                "Naredni preporučeni termin: jutro, 05:00–08:00.";
        }

        if (time <= morningEnd)
        {
            return
                "Trenutno je preporučeni jutarnji termin za zalijevanje.";
        }

        if (time < eveningStart)
        {
            return
                "Naredni preporučeni termin: večer, 19:00–22:00.";
        }

        if (time <= eveningEnd)
        {
            return
                "Trenutno je preporučeni večernji termin za zalijevanje.";
        }

        return
            "Naredni preporučeni termin: sutra ujutro, 05:00–08:00.";
    }

    private static string BuildDecisionReason(
        WateringRuleInput input,
        WateringDecision decision)
    {
        return
            $"soil={input.CurrentSoilMoisture?.ToString("0.#") ?? "n/a"}%; " +
            $"min={input.MinRecommendedSoilMoisture?.ToString() ?? "n/a"}%; " +
            $"critical={decision.CriticalMoistureThreshold}%; " +
            $"deficit={decision.MoistureDeficit:0.#}pp; " +
            $"criticalState={decision.IsCriticalMoisture}; " +
            $"baseAmount={decision.BaseAmountMl}ml; " +
            $"rainExposed={input.IsRainExposed}; " +
            $"rain24h={input.RainAmountNext24hMm:0.#}mm; " +
            $"rainBeforeNextWindow={input.RainAmountBeforeNextWindowMm:0.#}mm; " +
            $"diseaseModifier={input.DiseaseWateringModifier:0.00}x; " +
            $"recommended={decision.RecommendedAmountMl}ml; " +
            $"waitingForRain={decision.WaitingForRain}; " +
            $"autoAllowedNow={decision.IsAutomaticWateringAllowedNow}; " +
            $"requiresForce={decision.RequiresForce}";
    }
}