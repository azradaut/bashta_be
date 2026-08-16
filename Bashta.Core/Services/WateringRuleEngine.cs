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

    /*
     * Za sada ostavljamo properties radi kompatibilnosti
     * sa postojećim controllerima/DTO-ovima,
     * ali se više NE koriste kao blokada.
     */
    public int WateringCountLast24h { get; set; }

    public int MaxWateringCountLast24h { get; set; }

    /*
     * Kasnije ćemo ga popunjavati iz posljednje
     * relevantne detekcije bolesti.
     *
     * Primjer:
     * -10 = smanji količinu 10 %
     * +10 = povećaj količinu 10 %
     * 0   = bez korekcije
     */
    public int DiseaseWateringModifierPercent { get; set; }

    public string? ActiveDiseaseName { get; set; }
    public bool RainExpectedBeforeNextWindow { get; set; }

    public decimal RainAmountBeforeNextWindowMm { get; set; }

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
     * Kasnije ih u radu opisujemo kao konfiguracijske
     * parametre prototipske decision-support logike.
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
     * Ako je jako toplo, osnovna količina može
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

                RainIntensity =
                    rainForecast
                        ? "light"
                        : "none",

                IsRainExposed = false,

                HeatRiskNext24h =
                    temperature is >= 30m,

                DiseaseWateringModifierPercent = 0,

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
         * NAMJERNO nema više:
         *
         * if (WateringCountLast24h >= 2) ...
         *
         * Novo senzorsko stanje je glavni kriterij.
         */

        // 2. Bez senzorskog očitanja nema automatske odluke
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

        // 3. Mora postojati preporučeni minimum
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

        /*
         * 4. Kritični prag
         *
         * Primjer:
         * minimum = 70
         * critical = 55
         */
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

        /*
         * 5. Ako je vlaga već u ili iznad
         * preporučenog minimuma -> nema potrebe.
         */
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
                    "Vlažnost tla je iznad preporučenog opsega. Zalijevanje trenutno nije preporučeno.";
            }
            else
            {
                decision.StatusMessage =
                    "Vlažnost tla je u preporučenom opsegu. Zalijevanje trenutno nije potrebno.";
            }

            decision.WeatherImpactMessage =
                BuildNoWaterWeatherMessage(
                    input);

            decision.DecisionReason =
                BuildDecisionReason(
                    input,
                    decision);

            return decision;
        }

        /*
         * 6. Deficit vlage
         */
        var deficit =
            minMoisture -
            currentMoisture;

        decision.MoistureDeficit =
            deficit;

        /*
         * 7. Proporcionalni proračun
         *
         * Q = 50 + deficit * 10
         *
         * Primjer:
         * min = 70
         * current = 42
         * deficit = 28
         * Q = 330 ml
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

        /*
         * 8. Korekcija zbog toplote
         */
        if (IsHeatRisk(input))
        {
            adjustedAmount *=
                HeatMultiplier;
        }

        /*
         * 9. Kiša
         */
        ApplyRainLogic(
            input,
            decision,
            ref adjustedAmount);

        /*
         * Ako kiša nalaže potpuno čekanje,
         * nema potrebe za daljim korekcijama.
         */
        if (decision.WaitingForRain)
        {
            decision.RecommendedAmountMl = 0;

            decision.IsWateringRecommended = false;

            decision.RequiresForce = true;

            decision.IsAutomaticWateringAllowedNow = false;

            decision.StatusMessage =
                "Vlažnost tla je ispod preporučenog nivoa, ali nije u kritičnoj zoni. " +
                "Automatsko zalijevanje je privremeno odgođeno zbog očekivanih padavina.";

            decision.DecisionReason =
                BuildDecisionReason(
                    input,
                    decision);

            return decision;
        }

        /*
         * 10. Disease modifier
         *
         * Za sada će biti 0.
         * Kasnije ćemo ga puniti samo za bolesti
         * za koje imamo opravdanu konfiguraciju.
         */
        ApplyDiseaseAdjustment(
            input,
            decision,
            ref adjustedAmount);

        /*
         * 11. Konačna sigurnosna granica
         */
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

        /*
         * 12. Termin i jako sunce
         */
        var isPreferredTime =
            IsPreferredWateringTime(
                input.LocalNow);

        var isStrongSun =
            IsStrongSun(
                input.LocalNow,
                input.CurrentLux);

        if (isStrongSun)
        {
            decision.WarningMessage =
                "Trenutno nije idealno vrijeme za zalijevanje zbog jakog sunca. " +
                "Preporučuje se zalijevanje ujutro ili navečer.";

            /*
             * Kritično niska vlaga:
             * ne blokiramo intervenciju samo zbog termina.
             *
             * Nekritična:
             * automatski čekamo bolji termin.
             */
            if (!decision.IsCriticalMoisture)
            {
                decision.RequiresForce =
                    true;
            }
        }

        if (decision.IsCriticalMoisture &&
    !isPreferredTime)
        {
            decision.WarningMessage =
                "Vlažnost tla je u kritičnoj zoni. " +
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

        /*
         * Saksija koja ne kisne ne može koristiti
         * padavine kao zamjenu za zalijevanje.
         */
        if (!input.IsRainExposed)
        {
            decision.WeatherImpactMessage =
                input.RainExpectedIn24h
                    ? "Kiša je očekivana, ali saksija nije izložena padavinama. Prognoza ne mijenja količinu zalijevanja."
                    : "Kiša nije očekivana. Vremenski kontekst ne zahtijeva korekciju količine.";

            return;
        }

        /*
         * Za odluku nas zanima kiša DO NAREDNOG
         * preporučenog termina, a ne samo činjenica
         * da će možda pasti u naredna 24 sata.
         */
        if (!input.RainExpectedBeforeNextWindow ||
            input.RainAmountBeforeNextWindowMm <= 0)
        {
            decision.WeatherImpactMessage =
                input.RainExpectedIn24h
                    ? "Padavine se očekuju u naredna 24 sata, ali ne prije narednog preporučenog termina zalijevanja."
                    : "Kiša nije očekivana prije narednog preporučenog termina zalijevanja.";

            return;
        }

        var rain =
            input.RainAmountBeforeNextWindowMm;

        /*
         * < 1 mm:
         * vrlo mala količina ne utiče na odluku.
         */
        if (rain < 1m)
        {
            decision.WeatherImpactMessage =
                $"Do narednog termina očekuje se samo {rain:0.#} mm padavina. " +
                "Količina je premala da bi uticala na odluku zalijevanja.";

            return;
        }

        /*
         * 1–3 mm:
         * mala korekcija, ali ne odgađamo potpuno.
         */
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
         * >= 3 mm i vlaga NIJE kritična:
         * možemo čekati kišu.
         */
        if (!decision.IsCriticalMoisture)
        {
            decision.WaitingForRain =
                true;

            decision.WeatherImpactMessage =
                $"Do narednog preporučenog termina očekuje se {rain:0.#} mm padavina. " +
                "Vlažnost tla je ispod optimuma, ali nije kritično niska, pa se automatsko zalijevanje odgađa.";

            return;
        }

        /*
         * Kritično suho tlo:
         * nikad ne čekamo potpuno.
         *
         * 3–7 mm -> 70 % osnovne količine
         * >= 7 mm -> 60 % osnovne količine
         */
        if (rain < 7m)
        {
            amount *= 0.70m;

            decision.WeatherImpactMessage =
                $"Do narednog termina očekuje se {rain:0.#} mm padavina, " +
                "ali je vlažnost tla kritično niska. " +
                "Zalijevanje se izvršava smanjenom količinom umjesto potpunog odgađanja.";

            return;
        }

        amount *= 0.60m;

        decision.WeatherImpactMessage =
            $"Do narednog termina očekuje se {rain:0.#} mm obilnijih padavina, " +
            "ali je vlažnost tla kritično niska. " +
            "Primjenjuje se ograničena interventna količina vode do očekivanih padavina.";
    }

    private static void ApplyDiseaseAdjustment(
        WateringRuleInput input,
        WateringDecision decision,
        ref decimal amount)
    {
        if (input.DiseaseWateringModifierPercent == 0)
        {
            decision.DiseaseImpactMessage =
                string.IsNullOrWhiteSpace(
                    input.ActiveDiseaseName)
                    ? "Nema aktivne korekcije zalijevanja povezane sa zdravstvenim stanjem biljke."
                    : $"Detektovano stanje \"{input.ActiveDiseaseName}\" nema definisanu korekciju zalijevanja.";

            return;
        }

        /*
         * Sigurnosno ograničenje:
         * bolest smije korigovati najviše ±20 %.
         */
        var modifier =
            Math.Clamp(
                input.DiseaseWateringModifierPercent,
                -20,
                20);

        amount *=
            1m +
            modifier / 100m;

        decision.DiseaseImpactMessage =
            modifier > 0
                ? $"Zbog detektovanog zdravstvenog stanja količina vode je povećana za {modifier}%."
                : $"Zbog detektovanog zdravstvenog stanja količina vode je smanjena za {Math.Abs(modifier)}%.";
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
                "Očekuju se padavine, a trenutna vlažnost tla je već zadovoljavajuća. Zalijevanje nije potrebno.";
        }

        return
            "Vremenski uslovi trenutno ne zahtijevaju dodatnu korekciju odluke.";
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
            (time >= morningStart &&
             time <= morningEnd)
            ||
            (time >= eveningStart &&
             time <= eveningEnd);
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
        WateringDecision decision) => $"soil={input.CurrentSoilMoisture?.ToString("0.#") ?? "n/a"}%; " +
            $"min={input.MinRecommendedSoilMoisture?.ToString() ?? "n/a"}%; " +
            $"critical={decision.CriticalMoistureThreshold}%; " +
            $"deficit={decision.MoistureDeficit:0.#}pp; " +
            $"criticalState={decision.IsCriticalMoisture}; " +
            $"baseAmount={decision.BaseAmountMl}ml; " +
            $"rainExposed={input.IsRainExposed}; " +
            $"rain24h={input.RainAmountNext24hMm:0.#}mm; " +
            $"rainBeforeNextWindow={input.RainAmountBeforeNextWindowMm:0.#}mm; " +
            $"diseaseModifier={input.DiseaseWateringModifierPercent}%; " +
            $"recommended={decision.RecommendedAmountMl}ml; " +
            $"waitingForRain={decision.WaitingForRain}; " +
            $"autoAllowedNow={decision.IsAutomaticWateringAllowedNow}; " +
            $"requiresForce={decision.RequiresForce}";
}