using System.Globalization;
using System.Text;
using System.Text.Json;
using Bashta.Core.Services;
using Npgsql;

namespace Bashta.DataSeeder;

internal static class Program
{
    private const int UserId = 1;
    private const int Pot1Id = 1;   // Balkonski paradajz
    private const int Pot2Id = 12;  // Paradajz zuti - natkrivena saksija
    private const int Pot3Id = 15;  // Krovni paradajz
    private const int RandomSeed = 2026;
    private const decimal LowWaterThreshold = 15m;
    private const int ReservoirCapacityMl = 5000;

    private static readonly DateTimeOffset SeedStartLocal = new(2026, 6, 20, 0, 0, 0, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset SeedEndLocal = new(2026, 8, 20, 11, 0, 0, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset SyntheticCreatedLocal = new(2026, 6, 10, 12, 0, 0, TimeSpan.FromHours(2));

    private static readonly DateTimeOffset Pot1LateBlightStart = Local(2026, 7, 25, 18, 0);
    private static readonly DateTimeOffset Pot1HealthyAgain = Local(2026, 8, 2, 18, 0);
    private static readonly DateTimeOffset Pot3SpiderStart = Local(2026, 8, 5, 18, 0);
    private static readonly DateTimeOffset Pot3HealthyAgain = Local(2026, 8, 7, 0, 0);
    private static readonly DateTimeOffset Pot3LowWaterRefill = Local(2026, 8, 7, 12, 0);

    private static TimestampKind WateringEventTimestampKind = TimestampKind.WithTimeZone;
    private static TimestampKind DiseaseDetectionTimestampKind = TimestampKind.WithTimeZone;
    private static TimestampKind NotificationTimestampKind = TimestampKind.WithTimeZone;
    private static TimestampKind RecommendationTimestampKind = TimestampKind.WithTimeZone;

    private static readonly IReadOnlyList<RainEvent> RainSchedule =
    [
        new(new DateOnly(2026, 6, 24), 2.0m, new TimeOnly(18, 30)),
        new(new DateOnly(2026, 7, 3), 1.2m, new TimeOnly(6, 45)),
        new(new DateOnly(2026, 7, 12), 4.0m, new TimeOnly(7, 15)),
        new(new DateOnly(2026, 7, 29), 6.0m, new TimeOnly(20, 0)),
        new(new DateOnly(2026, 8, 4), 2.2m, new TimeOnly(6, 45)),
        new(new DateOnly(2026, 8, 10), 5.0m, new TimeOnly(7, 15)),
        new(new DateOnly(2026, 8, 12), 8.0m, new TimeOnly(7, 15)),
        new(new DateOnly(2026, 8, 16), 3.5m, new TimeOnly(19, 30))
    ];

    private static readonly IReadOnlyList<ScenarioDefinition> Scenarios =
    [
        new("S1", 1, Local(2026, 7, 2, 6, 30), 76m, null, 21m, 78m, 2500, null, "Normalno stanje; zalijevanje nije potrebno."),
        new("S2", 1, Local(2026, 7, 5, 6, 30), 60m, null, 22m, 72m, 3000, null, "Umjereno suho tlo u jutarnjem terminu; osnovna preporuka bez korekcija."),
        new("S3", 1, Local(2026, 7, 12, 6, 30), 60m, null, 22m, 74m, 3000, 4m, "Kiša prije kraja jutarnjeg termina i saksija izložena padavinama; automatsko zalijevanje se odgađa."),
        new("S4", 2, Local(2026, 7, 12, 6, 30), 60m, null, 22m, 74m, 3000, 4m, "Ista prognoza kao S3, ali natkrivena saksija; kiša ne mijenja količinu."),
        new("S5", 2, Local(2026, 7, 18, 6, 30), 60m, null, 31m, 48m, 3200, null, "Toplotni rizik povećava preporučenu količinu."),
        new("S6", 1, Local(2026, 7, 21, 13, 0), 60m, null, 28m, 45m, 25000, null, "Nekritično suho tlo pri jakom suncu; preporuka postoji, automatska intervencija se odgađa."),
        new("S7", 1, Local(2026, 7, 22, 13, 0), 50m, null, 28m, 44m, 25000, null, "Kritično suho tlo može nadjačati nepovoljan termin i jako sunce."),
        new("S8", 1, Local(2026, 7, 27, 6, 30), 60m, null, 22m, 75m, 3000, null, "Aktivna Late Blight detekcija primjenjuje negativni disease modifier."),
        new("S9", 3, Local(2026, 8, 6, 6, 0), 60m, null, 22m, 73m, 2800, null, "Aktivna Spider Mites detekcija primjenjuje pozitivni disease modifier."),
        new("S10", 3, Local(2026, 8, 7, 6, 0), 50m, 10m, 23m, 70m, 2500, null, "Kritično suho tlo, ali nivo vode u rezervoaru je ispod 15%; zalijevanje je blokirano."),
        new("S11", 1, Local(2026, 8, 10, 6, 30), 50m, null, 23m, 72m, 2500, 5m, "Kritično suho tlo uz 5 mm očekivane kiše; intervencija se ne odgađa potpuno nego se količina smanjuje."),
        new("S12", 1, Local(2026, 8, 12, 6, 30), 50m, null, 23m, 72m, 2500, 8m, "Kritično suho tlo uz 8 mm očekivane kiše; primjenjuje se ograničena interventna količina.")
    ];

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            var mode = ParseMode(args);
            var connectionString = ResolveConnectionString(args);

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            Console.WriteLine($"Povezano na PostgreSQL: {connection.Database}@{connection.Host}:{connection.Port}");
            await ValidateSchemaAsync(connection);

            if (mode is SeedMode.Reset or SeedMode.Reseed)
            {
                await ResetSyntheticDataAsync(connection);
                Console.WriteLine("Sintetički dataset je očišćen.");

                if (mode == SeedMode.Reset) return 0;
            }

            await SeedAsync(connection);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("GREŠKA:");

            if (ex is PostgresException pg)
            {
                Console.Error.WriteLine($"SqlState: {pg.SqlState}");
                Console.Error.WriteLine($"Message: {pg.MessageText}");
                Console.Error.WriteLine($"Detail: {pg.Detail}");
                Console.Error.WriteLine($"Where: {pg.Where}");
                Console.Error.WriteLine($"Schema: {pg.SchemaName}");
                Console.Error.WriteLine($"Table: {pg.TableName}");
                Console.Error.WriteLine($"Constraint: {pg.ConstraintName}");
                Console.Error.WriteLine($"Routine: {pg.Routine}");
            }

            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task SeedAsync(NpgsqlConnection connection)
    {
        Console.WriteLine("[1/10] Čitam Tomato plant_type...");
        var tomato = await GetTomatoPlantTypeAsync(connection);
        Console.WriteLine("[2/10] Čitam Late Blight disease...");
        var lateBlight = await FindDiseaseAsync(connection, "%late%blight%", "%kasna%", "Late Blight");
        Console.WriteLine("[3/10] Čitam Spider Mites disease...");
        var spiderMites = await FindDiseaseAsync(connection, "%spider%", "%grinj%", "Spider Mites");

        if (tomato.MinSoilMoisture != 70)
        {
            Console.WriteLine($"UPOZORENJE: Tomato MinSoilMoisture je {tomato.MinSoilMoisture}, a kontrolisani scenariji su projektovani za 70%.");
        }

        if (lateBlight.Modifier != 0.90m)
            Console.WriteLine($"UPOZORENJE: Late Blight modifier u bazi je {lateBlight.Modifier:0.00}, ne 0.90.");

        if (spiderMites.Modifier != 1.10m)
            Console.WriteLine($"UPOZORENJE: Spider Mites modifier u bazi je {spiderMites.Modifier:0.00}, ne 1.10.");

        Console.WriteLine("[4/10] Provjeravam user_id=1...");
        await EnsureUserExistsAsync(connection);

        Console.WriteLine("[5/10] Konfigurišem pot 1 (Balkonski paradajz) i aktivnu biljku...");
        var pot1 = await ConfigureExistingPotAsync(
            connection, role: 1, potId: Pot1Id, location: "Balkon, jug",
            rainExposed: true, intervalMinutes: 15, tomatoPlantTypeId: tomato.Id);

        Console.WriteLine("[6/10] Konfigurišem pot 2 (Paradajz zuti - natkrivena saksija) i aktivnu biljku...");
        var pot2 = await ConfigureExistingPotAsync(
            connection, role: 2, potId: Pot2Id, location: "Balkon - natkriveni dio",
            rainExposed: false, intervalMinutes: 30, tomatoPlantTypeId: tomato.Id);

        Console.WriteLine("[7/10] Konfigurišem pot 3 (Krovni paradajz) i aktivnu biljku...");
        var pot3 = await ConfigureExistingPotAsync(
            connection, role: 3, potId: Pot3Id, location: "Krovna terasa",
            rainExposed: true, intervalMinutes: 60, tomatoPlantTypeId: tomato.Id);

        var pots = new Dictionary<int, PotInfo>
        {
            [1] = pot1,
            [2] = pot2,
            [3] = pot3
        };

        if (await HasSyntheticSensorRowsAsync(connection, pots.Values.Select(x => x.Id).ToArray()))
        {
            throw new InvalidOperationException(
                "U definisanom sintetičkom periodu već postoje sensor_readings zapisi. " +
                "Pokreni program sa --reseed ako želiš ponovo generisati kompletan skup.");
        }

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            Console.WriteLine("[8/10] Upisujem disease timeline...");
            await SeedDiseaseTimelineAsync(connection, transaction, pot1, pot3, lateBlight, spiderMites);

            var engine = new WateringRuleEngine();
            var random = new Random(RandomSeed);
            var results = new List<ScenarioResult>();

            Console.WriteLine("[9/10] Generišem sensor_readings za pot 1...");
            await GeneratePotHistoryAsync(connection, transaction, engine, random, pot1, tomato, lateBlight, spiderMites, results, initialSoil: 78m, initialWater: 92m);
            Console.WriteLine("[9/10] Generišem sensor_readings za pot 2...");
            await GeneratePotHistoryAsync(connection, transaction, engine, random, pot2, tomato, lateBlight, spiderMites, results, initialSoil: 74m, initialWater: 90m);
            Console.WriteLine("[9/10] Generišem sensor_readings za pot 3...");
            await GeneratePotHistoryAsync(connection, transaction, engine, random, pot3, tomato, lateBlight, spiderMites, results, initialSoil: 72m, initialWater: 62m);

            Console.WriteLine("[10/10] Commit...");
            await transaction.CommitAsync();

            WriteScenarioResults(results);
            await PrintSummaryAsync(connection, pots.Values);

            Console.WriteLine();
            Console.WriteLine("Seed završen.");
            Console.WriteLine("Sljedeći korak: pokreni Wokwi nakon 20.08.2026. 11:00 kako bi se nova integracijska očitanja prirodno nastavila na sintetičku historiju.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task GeneratePotHistoryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WateringRuleEngine engine,
        Random random,
        PotInfo pot,
        PlantTypeInfo tomato,
        DiseaseInfo lateBlight,
        DiseaseInfo spiderMites,
        List<ScenarioResult> scenarioResults,
        decimal initialSoil,
        decimal initialWater)
    {
        var state = new PotState(initialSoil, initialWater);
        var scenarios = Scenarios.Where(x => x.PotRole == pot.Role).OrderBy(x => x.LocalTime).ToList();
        var appliedRainDates = new HashSet<DateOnly>();
        var lowWaterNotificationCreated = false;

        await using var sensorCmd = new NpgsqlCommand(
            """
            INSERT INTO sensor_readings
                (time, pot_id, soil_moisture, temperature, humidity, lux, water_level)
            VALUES
                (@time, @potId, @soil, @temperature, @humidity, @lux, @waterLevel);
            """,
            connection,
            transaction);

        sensorCmd.Parameters.Add(new NpgsqlParameter("time", NpgsqlTypes.NpgsqlDbType.TimestampTz));
        sensorCmd.Parameters.Add(new NpgsqlParameter("potId", NpgsqlTypes.NpgsqlDbType.Integer));
        sensorCmd.Parameters.Add(new NpgsqlParameter("soil", NpgsqlTypes.NpgsqlDbType.Numeric));
        sensorCmd.Parameters.Add(new NpgsqlParameter("temperature", NpgsqlTypes.NpgsqlDbType.Numeric));
        sensorCmd.Parameters.Add(new NpgsqlParameter("humidity", NpgsqlTypes.NpgsqlDbType.Numeric));
        sensorCmd.Parameters.Add(new NpgsqlParameter("lux", NpgsqlTypes.NpgsqlDbType.Integer));
        sensorCmd.Parameters.Add(new NpgsqlParameter("waterLevel", NpgsqlTypes.NpgsqlDbType.Numeric));
        await sensorCmd.PrepareAsync();

        var current = SeedStartLocal;
        var stepHours = pot.IntervalMinutes / 60m;

        while (current <= SeedEndLocal)
        {
            ApplyRainToSoilIfDue(current, pot, state, appliedRainDates);
            ApplyRefillPolicy(current, pot, state, random);

            var ambient = BuildAmbient(current, random);
            ApplySoilDrying(state, ambient, pot, stepHours, random);
            NudgeSoilTowardUpcomingScenario(current, pot.Role, scenarios, state, stepHours);

            var scenario = scenarios.FirstOrDefault(x => x.LocalTime == current);

            if (scenario is not null)
            {
                state.Soil = scenario.SoilMoisture;
                if (scenario.WaterLevel.HasValue) state.Water = scenario.WaterLevel.Value;
                ambient = new Ambient(scenario.Temperature, scenario.Humidity, scenario.Lux);
            }

            state.Soil = Clamp(state.Soil, 30m, 94m);
            state.Water = Clamp(state.Water, 0m, 100m);

            sensorCmd.Parameters["time"].Value = current.UtcDateTime;
            sensorCmd.Parameters["potId"].Value = pot.Id;
            sensorCmd.Parameters["soil"].Value = Round1(state.Soil);
            sensorCmd.Parameters["temperature"].Value = Round1(ambient.Temperature);
            sensorCmd.Parameters["humidity"].Value = Round1(ambient.Humidity);
            sensorCmd.Parameters["lux"].Value = ambient.Lux;
            sensorCmd.Parameters["waterLevel"].Value = Round1(state.Water);
            await sensorCmd.ExecuteNonQueryAsync();

            var sensorSoil = Round1(state.Soil);
            var sensorWater = Round1(state.Water);

            var diseaseContext = GetDiseaseContext(pot.Role, current, lateBlight, spiderMites);
            var weather = scenario?.RainBeforeNextWindowMm is decimal scenarioRain
                ? BuildScenarioWeather(current, scenarioRain)
                : BuildWeatherContext(current);

            var decision = engine.Evaluate(new WateringRuleInput
            {
                PlantId = pot.PlantId,
                CurrentSoilMoisture = sensorSoil,
                MinRecommendedSoilMoisture = tomato.MinSoilMoisture,
                MaxRecommendedSoilMoisture = tomato.MaxSoilMoisture,
                CurrentTemperature = Round1(ambient.Temperature),
                MaxTemperatureNext24h = weather.MaxTemperatureNext24h,
                CurrentLux = ambient.Lux,
                IsRainExposed = pot.IsRainExposed,
                WeatherAvailable = true,
                RainExpectedIn24h = weather.RainExpectedIn24h,
                RainAmountNext24hMm = weather.RainAmountNext24hMm,
                RainExpectedBeforeNextWindow = weather.RainExpectedBeforeNextWindow,
                RainAmountBeforeNextWindowMm = weather.RainAmountBeforeNextWindowMm,
                RainIntensity = weather.RainIntensity,
                HeatRiskNext24h = weather.HeatRiskNext24h || ambient.Temperature >= 30m,
                DiseaseWateringModifier = diseaseContext.Modifier,
                ActiveDiseaseName = diseaseContext.Name,
                LocalNow = current.DateTime
            });

            var reservoirLow = sensorWater <= LowWaterThreshold;

            if (reservoirLow && decision.IsWateringRecommended)
            {
                var ruleEngineAutoAllowedNow = decision.IsAutomaticWateringAllowedNow;

                decision.CanWater = false;
                decision.IsAutomaticWateringAllowedNow = false;
                decision.WarningMessage =
                    $"Automatsko zalijevanje nije izvršeno jer je nivo vode u rezervoaru {sensorWater:0.#}%. " +
                    $"Za zalijevanje je potreban nivo vode iznad {LowWaterThreshold:0.#}%.";

                decision.DecisionReason =
                    decision.DecisionReason.Replace(
                        $"autoAllowedNow={ruleEngineAutoAllowedNow}",
                        $"ruleEngineAutoAllowedNow={ruleEngineAutoAllowedNow}") +
                    $"; waterLevel={sensorWater:0.#}%; reservoirLow=True; finalAutoAllowedNow=False";
            }

            var isControlledScenario = scenario is not null;
            var shouldCreateSkippedScenarioEvent =
                isControlledScenario &&
                (scenario!.Id is "S3" or "S6" or "S10");

            if (decision.IsAutomaticWateringAllowedNow && state.Water > LowWaterThreshold)
            {
                var amount = Math.Max(0, decision.RecommendedAmountMl);
                await InsertWateringEventAsync(connection, transaction, pot.Id, current.UtcDateTime, sensorSoil, amount, false, null, decision, weather.Summary, scenario?.Id);
                ApplyWateringToState(state, amount);
            }
            else if (shouldCreateSkippedScenarioEvent)
            {
                var reason = decision.WarningMessage ?? decision.WeatherImpactMessage ?? decision.StatusMessage;
                await InsertWateringEventAsync(connection, transaction, pot.Id, current.UtcDateTime, sensorSoil, 0, true, reason, decision, weather.Summary, scenario!.Id);
            }

            if (scenario?.Id == "S10" && reservoirLow && !lowWaterNotificationCreated)
            {
                await InsertLowWaterNotificationAsync(connection, transaction, pot, current.UtcDateTime, sensorWater);
                lowWaterNotificationCreated = true;
            }

            if (scenario is not null)
            {
                scenarioResults.Add(new ScenarioResult(
                    scenario.Id,
                    pot.Id,
                    pot.Name,
                    current,
                    sensorSoil,
                    sensorWater,
                    diseaseContext.Name ?? "Healthy",
                    diseaseContext.Modifier,
                    weather.RainAmountBeforeNextWindowMm,
                    decision.IsWateringRecommended,
                    decision.RecommendedAmountMl,
                    decision.IsAutomaticWateringAllowedNow,
                    decision.WaitingForRain,
                    decision.StatusMessage,
                    decision.WarningMessage,
                    decision.WeatherImpactMessage,
                    decision.DiseaseImpactMessage,
                    scenario.Note));
            }

            current = current.AddMinutes(pot.IntervalMinutes);
        }
    }

    private static Ambient BuildAmbient(DateTimeOffset local, Random random)
    {
        var hour = local.Hour + local.Minute / 60.0;
        var days = (local.Date - SeedStartLocal.Date).TotalDays;
        var mean = 23.5 - 0.025 * days;
        var diurnal = 6.4 * Math.Sin(2.0 * Math.PI * (hour - 8.0) / 24.0);
        var heatwave = GetHeatwaveBoost(DateOnly.FromDateTime(local.DateTime));
        var temperature = mean + diurnal + heatwave + NextNoise(random, 1.1);
        temperature = Math.Clamp(temperature, 14.0, 36.0);

        var humidity = 72.0 - (temperature - 18.0) * 1.7 + NextNoise(random, 5.0);
        if (hour < 7 || hour > 21) humidity += 7.0;
        humidity = Math.Clamp(humidity, 35.0, 94.0);

        var rain = RainSchedule.FirstOrDefault(x => x.Date == DateOnly.FromDateTime(local.DateTime));
        var cloudFactor = rain is null ? 0.72 + random.NextDouble() * 0.28 : 0.28 + random.NextDouble() * 0.30;

        int lux;
        const double sunrise = 5.3;
        const double sunset = 20.5;

        if (hour < sunrise || hour > sunset)
        {
            lux = random.Next(0, 20);
        }
        else
        {
            var sun = Math.Sin(Math.PI * (hour - sunrise) / (sunset - sunrise));
            lux = (int)Math.Round(Math.Max(80.0, 52000.0 * sun * cloudFactor));
        }

        return new Ambient((decimal)temperature, (decimal)humidity, lux);
    }

    private static void ApplySoilDrying(PotState state, Ambient ambient, PotInfo pot, decimal stepHours, Random random)
    {
        var exposure = pot.Role switch
        {
            2 => 0.88m,
            3 => 1.10m,
            _ => 1.00m
        };

        var heat = Math.Max(0m, ambient.Temperature - 22m) * 0.018m;
        var light = Math.Min(1m, ambient.Lux / 50000m) * 0.10m;
        var lossPerHour = (0.18m + heat + light) * exposure;
        state.Soil -= lossPerHour * stepHours;
        state.Soil += (decimal)NextNoise(random, 0.08);

        state.Water -= 0.003m * stepHours;
    }

    private static void NudgeSoilTowardUpcomingScenario(DateTimeOffset current, int potRole, IReadOnlyList<ScenarioDefinition> scenarios, PotState state, decimal stepHours)
    {
        var next = scenarios.FirstOrDefault(x => x.LocalTime > current && x.LocalTime - current <= TimeSpan.FromHours(12));
        if (next is null) return;

        var remainingHours = Math.Max(0.25, (next.LocalTime - current).TotalHours);
        var idealStep = (next.SoilMoisture - state.Soil) / (decimal)Math.Max(1.0, remainingHours / (double)stepHours);
        state.Soil += Math.Clamp(idealStep, -0.45m, 0.45m);
    }

    private static void ApplyRainToSoilIfDue(DateTimeOffset current, PotInfo pot, PotState state, HashSet<DateOnly> appliedDates)
    {
        if (!pot.IsRainExposed) return;

        var date = DateOnly.FromDateTime(current.DateTime);
        var rain = RainSchedule.FirstOrDefault(x => x.Date == date);
        if (rain is null || appliedDates.Contains(date)) return;

        var rainTime = date.ToDateTime(rain.LocalTime);
        if (current.DateTime < rainTime) return;

        state.Soil = Clamp(state.Soil + rain.AmountMm * 1.25m, 30m, 94m);
        appliedDates.Add(date);
    }

    private static void ApplyRefillPolicy(DateTimeOffset current, PotInfo pot, PotState state, Random random)
    {
        if (pot.Role == 3)
        {
            if (current < Local(2026, 8, 7, 6, 0) && state.Water < 16.5m) state.Water = 16.5m;
            if (current >= Pot3LowWaterRefill && state.Water <= LowWaterThreshold) state.Water = 92m;
            return;
        }

        if (state.Water <= 18m && current.Hour is >= 7 and <= 10)
            state.Water = 88m + (decimal)(random.NextDouble() * 8.0);
    }

    private static void ApplyWateringToState(PotState state, int amountMl)
    {
        if (amountMl <= 0) return;

        var soilIncrease = Math.Min(30m, 6m + amountMl / 10m);
        state.Soil = Clamp(state.Soil + soilIncrease, 30m, 94m);

        var percentUsed = amountMl * 100m / ReservoirCapacityMl;
        state.Water = Clamp(state.Water - percentUsed, 0m, 100m);
    }

    private static DiseaseContext GetDiseaseContext(int potRole, DateTimeOffset local, DiseaseInfo lateBlight, DiseaseInfo spiderMites)
    {
        if (potRole == 1 && local >= Pot1LateBlightStart && local < Pot1HealthyAgain)
            return new DiseaseContext(lateBlight.DisplayName, lateBlight.Modifier);

        if (potRole == 3 && local >= Pot3SpiderStart && local < Pot3HealthyAgain)
            return new DiseaseContext(spiderMites.DisplayName, spiderMites.Modifier);

        return new DiseaseContext(null, 1.00m);
    }

    private static WeatherContext BuildWeatherContext(DateTimeOffset local)
    {
        var next24 = RainSchedule
            .Select(x => new { Rain = x, Time = new DateTimeOffset(x.Date.ToDateTime(x.LocalTime), local.Offset) })
            .Where(x => x.Time > local && x.Time <= local.AddHours(24))
            .OrderBy(x => x.Time)
            .ToList();

        var rain24 = next24.Sum(x => x.Rain.AmountMm);
        var horizon = GetNextWateringDecisionHorizon(local);
        var beforeWindow = next24.Where(x => x.Time <= horizon).Sum(x => x.Rain.AmountMm);
        var maxTemp = GetExpectedDailyMax(DateOnly.FromDateTime(local.DateTime));
        var intensity = rain24 switch
        {
            >= 7m => "heavy",
            >= 3m => "moderate",
            > 0m => "light",
            _ => "none"
        };

        var summary = rain24 > 0
            ? $"Sintetički vremenski kontekst: očekivano {rain24:0.#} mm padavina u naredna 24 h; do narednog termina {beforeWindow:0.#} mm; prognozirani maksimum {maxTemp:0.#} °C."
            : $"Sintetički vremenski kontekst: bez očekivanih padavina u naredna 24 h; prognozirani maksimum {maxTemp:0.#} °C.";

        return new WeatherContext(
            rain24 > 0,
            rain24,
            beforeWindow > 0,
            beforeWindow,
            intensity,
            maxTemp,
            maxTemp >= 32m,
            summary);
    }

    private static WeatherContext BuildScenarioWeather(DateTimeOffset local, decimal rainBeforeNextWindowMm)
    {
        var maxTemp = GetExpectedDailyMax(DateOnly.FromDateTime(local.DateTime));
        var intensity = rainBeforeNextWindowMm switch
        {
            >= 7m => "heavy",
            >= 3m => "moderate",
            > 0m => "light",
            _ => "none"
        };

        return new WeatherContext(
            rainBeforeNextWindowMm > 0,
            rainBeforeNextWindowMm,
            rainBeforeNextWindowMm > 0,
            rainBeforeNextWindowMm,
            intensity,
            maxTemp,
            maxTemp >= 32m,
            $"Kontrolisani vremenski scenario: {rainBeforeNextWindowMm:0.#} mm padavina prije kraja narednog preporučenog termina; prognozirani maksimum {maxTemp:0.#} °C.");
    }

    private static DateTimeOffset GetNextWateringDecisionHorizon(DateTimeOffset local)
    {
        var time = local.TimeOfDay;
        var morningStart = new TimeSpan(5, 0, 0);
        var morningEnd = new TimeSpan(8, 0, 0);
        var eveningStart = new TimeSpan(19, 0, 0);
        var eveningEnd = new TimeSpan(22, 0, 0);

        if (time < morningStart) return AtLocal(local, 8);
        if (time <= morningEnd) return AtLocal(local, 8);
        if (time < eveningStart) return AtLocal(local, 22);
        if (time <= eveningEnd) return AtLocal(local, 22);
        return AtLocal(local.AddDays(1), 8);
    }

    private static DateTimeOffset AtLocal(DateTimeOffset local, int hour) =>
        new(local.Year, local.Month, local.Day, hour, 0, 0, local.Offset);

    private static decimal GetExpectedDailyMax(DateOnly date)
    {
        var days = date.DayNumber - new DateOnly(2026, 6, 20).DayNumber;
        var value = 29.8m - (decimal)days * 0.015m + (decimal)GetHeatwaveBoost(date);
        return Math.Clamp(value, 27m, 35m);
    }

    private static double GetHeatwaveBoost(DateOnly date)
    {
        if (date >= new DateOnly(2026, 7, 16) && date <= new DateOnly(2026, 7, 20)) return 4.0;
        if (date >= new DateOnly(2026, 8, 4) && date <= new DateOnly(2026, 8, 6)) return 2.5;
        return 0.0;
    }

    private static async Task InsertWateringEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int potId,
        DateTime createdAtUtc,
        decimal soil,
        int amountMl,
        bool skipped,
        string? skipReason,
        WateringDecision decision,
        string weatherSummary,
        string? scenarioId)
    {
        var decisionReason = decision.DecisionReason;
        if (!string.IsNullOrWhiteSpace(scenarioId)) decisionReason += $"; syntheticScenario={scenarioId}";

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO watering_event
                (pot_id, triggered_by, duration_sec, amount_ml, soil_moisture_before,
                 soil_moisture_after, skipped, skip_reason, created_at, is_forced,
                 decision_reason, weather_summary)
            VALUES
                (@potId, 'auto', @durationSec, @amountMl, @soilBefore,
                 NULL, @skipped, @skipReason, @createdAt, false,
                 @decisionReason, @weatherSummary);
            """,
            connection,
            transaction);

        cmd.Parameters.AddWithValue("potId", potId);
        cmd.Parameters.Add("durationSec", NpgsqlTypes.NpgsqlDbType.Integer).Value = skipped ? DBNull.Value : 10;
        cmd.Parameters.AddWithValue("amountMl", amountMl);
        cmd.Parameters.AddWithValue("soilBefore", (int)Math.Round(soil));
        cmd.Parameters.AddWithValue("skipped", skipped);
        cmd.Parameters.Add("skipReason", NpgsqlTypes.NpgsqlDbType.Text).Value = (object?)skipReason ?? DBNull.Value;
        cmd.Parameters.AddWithValue("createdAt", ToDbTimestamp(createdAtUtc, WateringEventTimestampKind));
        cmd.Parameters.AddWithValue("decisionReason", decisionReason);
        cmd.Parameters.AddWithValue("weatherSummary", weatherSummary);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertLowWaterNotificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PotInfo pot,
        DateTime createdAtUtc,
        decimal waterLevel)
    {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO notification (user_id, title, body, type, is_read, created_at)
            VALUES (@userId, 'Nizak nivo vode', @body, 'alert', false, @createdAt);
            """,
            connection,
            transaction);

        cmd.Parameters.AddWithValue("userId", UserId);
        cmd.Parameters.AddWithValue("body", $"Nivo vode u rezervoaru saksije \"{pot.Name}\" je {waterLevel:0.#}%. Dopunite rezervoar prije narednog zalijevanja.");
        cmd.Parameters.AddWithValue("createdAt", ToDbTimestamp(createdAtUtc, NotificationTimestampKind));
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task SeedDiseaseTimelineAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PotInfo pot1,
        PotInfo pot3,
        DiseaseInfo lateBlight,
        DiseaseInfo spiderMites)
    {
        await InsertDiseaseDetectionAsync(connection, transaction, pot1.PlantId, lateBlight.Id, 0.93m, false, Pot1LateBlightStart.UtcDateTime);
        await InsertDiseaseDetectionAsync(connection, transaction, pot1.PlantId, null, 0.98m, true, Pot1HealthyAgain.UtcDateTime);
        await InsertDiseaseDetectionAsync(connection, transaction, pot3.PlantId, spiderMites.Id, 0.91m, false, Pot3SpiderStart.UtcDateTime);
        await InsertDiseaseDetectionAsync(connection, transaction, pot3.PlantId, null, 0.97m, true, Pot3HealthyAgain.UtcDateTime);
    }

    private static async Task InsertDiseaseDetectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int plantId,
        int? diseaseId,
        decimal confidence,
        bool isHealthy,
        DateTime createdAtUtc)
    {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO disease_detection
                (plant_id, disease_id, confidence, image_path, is_healthy, created_at)
            VALUES
                (@plantId, @diseaseId, @confidence, NULL, @isHealthy, @createdAt);
            """,
            connection,
            transaction);

        cmd.Parameters.AddWithValue("plantId", plantId);
        cmd.Parameters.Add("diseaseId", NpgsqlTypes.NpgsqlDbType.Integer).Value = (object?)diseaseId ?? DBNull.Value;
        cmd.Parameters.AddWithValue("confidence", confidence);
        cmd.Parameters.AddWithValue("isHealthy", isHealthy);
        cmd.Parameters.AddWithValue("createdAt", ToDbTimestamp(createdAtUtc, DiseaseDetectionTimestampKind));
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<PotInfo> ConfigureExistingPotAsync(
        NpgsqlConnection connection,
        int role,
        int potId,
        string location,
        bool rainExposed,
        int intervalMinutes,
        int tomatoPlantTypeId)
    {
        await using (var update = new NpgsqlCommand(
            """
            UPDATE plant_pot
            SET location = @location,
                is_active = true,
                is_rain_exposed = @rainExposed,
                sensor_reading_interval_minutes = @interval
            WHERE id = @id AND user_id = @userId;
            """,
            connection))
        {
            update.Parameters.AddWithValue("location", location);
            update.Parameters.AddWithValue("rainExposed", rainExposed);
            update.Parameters.AddWithValue("interval", intervalMinutes);
            update.Parameters.AddWithValue("id", potId);
            update.Parameters.AddWithValue("userId", UserId);

            if (await update.ExecuteNonQueryAsync() == 0)
            {
                throw new InvalidOperationException(
                    $"Nije pronađena plant_pot saksija id={potId} za user_id={UserId}.");
            }
        }

        var name = await ScalarStringAsync(
            connection,
            "SELECT name FROM plant_pot WHERE id = @id AND user_id = @userId;",
            ("id", potId),
            ("userId", UserId));

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                $"Saksija id={potId} nema validan naziv ili ne pripada user_id={UserId}.");
        }

        var plantId = await EnsureActivePlantAsync(
            connection,
            potId,
            tomatoPlantTypeId,
            name);

        return new PotInfo(
            role,
            potId,
            name,
            rainExposed,
            intervalMinutes,
            plantId);
    }

    private static async Task<int> EnsureActivePlantAsync(NpgsqlConnection connection, int potId, int plantTypeId, string nickname)
    {
        await using (var find = new NpgsqlCommand(
            """
            SELECT id
            FROM plant
            WHERE pot_id = @potId AND removed_at IS NULL
            ORDER BY planted_at DESC, id DESC
            LIMIT 1;
            """,
            connection))
        {
            find.Parameters.AddWithValue("potId", potId);
            var value = await find.ExecuteScalarAsync();
            if (value is not null && value is not DBNull)
            {
                var id = Convert.ToInt32(value, CultureInfo.InvariantCulture);

                await using var update = new NpgsqlCommand(
                    """
                    UPDATE plant
                    SET plant_type_id = @plantTypeId,
                        nickname = @nickname,
                        planted_at = LEAST(planted_at, DATE '2026-06-10')
                    WHERE id = @id;
                    """,
                    connection);
                update.Parameters.AddWithValue("plantTypeId", plantTypeId);
                update.Parameters.AddWithValue("nickname", nickname);
                update.Parameters.AddWithValue("id", id);
                await update.ExecuteNonQueryAsync();
                return id;
            }
        }

        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO plant (pot_id, plant_type_id, nickname, planted_at, removed_at, notes, image_path)
            VALUES (@potId, @plantTypeId, @nickname, DATE '2026-06-10', NULL, 'Sintetički historijski dataset za evaluaciju prototipa.', NULL)
            RETURNING id;
            """,
            connection);

        insert.Parameters.AddWithValue("potId", potId);
        insert.Parameters.AddWithValue("plantTypeId", plantTypeId);
        insert.Parameters.AddWithValue("nickname", nickname);
        return Convert.ToInt32(await insert.ExecuteScalarAsync() ?? throw new InvalidOperationException("Nije moguće kreirati biljku."), CultureInfo.InvariantCulture);
    }

    private static async Task<PlantTypeInfo> GetTomatoPlantTypeAsync(NpgsqlConnection connection)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, name, COALESCE(name_local, name), min_soil_moisture, max_soil_moisture
            FROM plant_type
            WHERE LOWER(name) LIKE '%tomato%' OR LOWER(COALESCE(name_local, '')) LIKE '%paradajz%'
            ORDER BY id
            LIMIT 1;
            """,
            connection);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("U plant_type tabeli nije pronađen Tomato/Paradajz.");

        return new PlantTypeInfo(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetInt32(4));
    }

    private static async Task<DiseaseInfo> FindDiseaseAsync(
        NpgsqlConnection connection,
        string englishPattern,
        string localPattern,
        string fallbackDisplayName)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, name, COALESCE(name_local, name), watering_modifier
            FROM disease
            WHERE LOWER(name) LIKE @englishPattern
               OR LOWER(COALESCE(name_local, '')) LIKE @localPattern
            ORDER BY id
            LIMIT 1;
            """,
            connection);

        cmd.Parameters.AddWithValue("englishPattern", englishPattern.ToLowerInvariant());
        cmd.Parameters.AddWithValue("localPattern", localPattern.ToLowerInvariant());

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException($"U disease tabeli nije pronađeno stanje: {fallbackDisplayName}.");

        return new DiseaseInfo(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3));
    }

    private static async Task EnsureUserExistsAsync(NpgsqlConnection connection)
    {
        await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE id = @id;", connection);
        cmd.Parameters.AddWithValue("id", UserId);
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0, CultureInfo.InvariantCulture);
        if (count == 0) throw new InvalidOperationException("Nije pronađen users red sa id=1.");
    }

    private static async Task ValidateSchemaAsync(NpgsqlConnection connection)
    {
        await using (var cmd = new NpgsqlCommand(
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_name = 'sensor_readings' AND column_name = 'water_level';
            """,
            connection))
        {
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0, CultureInfo.InvariantCulture);
            if (count == 0)
                throw new InvalidOperationException("sensor_readings.water_level kolona ne postoji. Pokreni lokalnu migraciju/ALTER prije seedovanja.");
        }

        WateringEventTimestampKind = await GetTimestampKindAsync(connection, "watering_event", "created_at");
        DiseaseDetectionTimestampKind = await GetTimestampKindAsync(connection, "disease_detection", "created_at");
        NotificationTimestampKind = await GetTimestampKindAsync(connection, "notification", "created_at");
        RecommendationTimestampKind = await GetTimestampKindAsync(connection, "recommendation", "created_at");
    }

    private static async Task<TimestampKind> GetTimestampKindAsync(NpgsqlConnection connection, string tableName, string columnName)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @tableName AND column_name = @columnName;
            """,
            connection);

        cmd.Parameters.AddWithValue("tableName", tableName);
        cmd.Parameters.AddWithValue("columnName", columnName);
        var dataType = (await cmd.ExecuteScalarAsync())?.ToString();

        return dataType switch
        {
            "timestamp with time zone" => TimestampKind.WithTimeZone,
            "timestamp without time zone" => TimestampKind.WithoutTimeZone,
            _ => throw new InvalidOperationException($"Nepodržan tip {tableName}.{columnName}: {dataType ?? "nije pronađen"}.")
        };
    }

    private static async Task<bool> HasSyntheticSensorRowsAsync(NpgsqlConnection connection, int[] potIds)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM sensor_readings
                WHERE pot_id = ANY(@potIds)
                  AND time >= @fromUtc
                  AND time <= @toUtc
            );
            """,
            connection);

        cmd.Parameters.AddWithValue("potIds", potIds);
        cmd.Parameters.AddWithValue("fromUtc", SeedStartLocal.UtcDateTime);
        cmd.Parameters.AddWithValue("toUtc", SeedEndLocal.UtcDateTime);
        return Convert.ToBoolean(await cmd.ExecuteScalarAsync() ?? false, CultureInfo.InvariantCulture);
    }

    private static async Task ResetSyntheticDataAsync(NpgsqlConnection connection)
    {
        await EnsureUserExistsAsync(connection);

        // Evaluacija koristi tri postojeće saksije iz baze.
        // ID-jevi su fiksirani kako reseed ne bi zavisio od njihovih naziva.
        var potIds = new List<int> { Pot1Id, Pot2Id, Pot3Id };

        var plantIds = new List<int>();
        await using (var findPlants = new NpgsqlCommand(
            "SELECT id FROM plant WHERE pot_id = ANY(@potIds);",
            connection))
        {
            findPlants.Parameters.AddWithValue("potIds", potIds.ToArray());
            await using var reader = await findPlants.ExecuteReaderAsync();
            while (await reader.ReadAsync()) plantIds.Add(reader.GetInt32(0));
        }

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            if (plantIds.Count > 0)
            {
                await ExecuteAsync(connection, transaction,
                    "DELETE FROM disease_detection WHERE plant_id = ANY(@plantIds) AND created_at >= @fromUtc AND created_at <= @toUtc;",
                    ("plantIds", plantIds.ToArray()),
                    ("fromUtc", ToDbTimestamp(SeedStartLocal.UtcDateTime, DiseaseDetectionTimestampKind)),
                    ("toUtc", ToDbTimestamp(SeedEndLocal.UtcDateTime, DiseaseDetectionTimestampKind)));

                await ExecuteAsync(connection, transaction,
                    "DELETE FROM recommendation WHERE plant_id = ANY(@plantIds) AND created_at >= @fromUtc AND created_at <= @toUtc;",
                    ("plantIds", plantIds.ToArray()),
                    ("fromUtc", ToDbTimestamp(SeedStartLocal.UtcDateTime, RecommendationTimestampKind)),
                    ("toUtc", ToDbTimestamp(SeedEndLocal.UtcDateTime, RecommendationTimestampKind)));
            }

            await ExecuteAsync(connection, transaction,
                "DELETE FROM watering_event WHERE pot_id = ANY(@potIds) AND created_at >= @fromUtc AND created_at <= @toUtc;",
                ("potIds", potIds.ToArray()),
                ("fromUtc", ToDbTimestamp(SeedStartLocal.UtcDateTime, WateringEventTimestampKind)),
                ("toUtc", ToDbTimestamp(SeedEndLocal.UtcDateTime, WateringEventTimestampKind)));

            await ExecuteAsync(connection, transaction,
                "DELETE FROM sensor_readings WHERE pot_id = ANY(@potIds) AND time >= @fromUtc AND time <= @toUtc;",
                ("potIds", potIds.ToArray()), ("fromUtc", SeedStartLocal.UtcDateTime), ("toUtc", SeedEndLocal.UtcDateTime));

            await ExecuteAsync(connection, transaction,
                "DELETE FROM notification WHERE user_id = @userId AND created_at >= @fromUtc AND created_at <= @toUtc;",
                ("userId", UserId),
                ("fromUtc", ToDbTimestamp(SeedStartLocal.UtcDateTime, NotificationTimestampKind)),
                ("toUtc", ToDbTimestamp(SeedEndLocal.UtcDateTime, NotificationTimestampKind)));

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task PrintSummaryAsync(NpgsqlConnection connection, IEnumerable<PotInfo> pots)
    {
        Console.WriteLine();
        Console.WriteLine("Sažetak generisanog skupa:");

        foreach (var pot in pots.OrderBy(x => x.Role))
        {
            await using var cmd = new NpgsqlCommand(
                """
                SELECT COUNT(*), MIN(time), MAX(time),
                       ROUND(MIN(soil_moisture), 2), ROUND(MAX(soil_moisture), 2),
                       ROUND(MIN(water_level), 2), ROUND(MAX(water_level), 2)
                FROM sensor_readings
                WHERE pot_id = @potId AND time >= @fromUtc AND time <= @toUtc;
                """,
                connection);

            cmd.Parameters.AddWithValue("potId", pot.Id);
            cmd.Parameters.AddWithValue("fromUtc", SeedStartLocal.UtcDateTime);
            cmd.Parameters.AddWithValue("toUtc", SeedEndLocal.UtcDateTime);

            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();

            Console.WriteLine(
                $"- pot {pot.Id} / {pot.Name}: interval {pot.IntervalMinutes} min, " +
                $"readings={reader.GetInt64(0)}, soil={reader.GetValue(3)}–{reader.GetValue(4)}%, water={reader.GetValue(5)}–{reader.GetValue(6)}%.");
        }
    }

    private static void WriteScenarioResults(IReadOnlyList<ScenarioResult> results)
    {
        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "seed-output");
        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, "scenario_results.csv");

        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine("Scenario;PotId;Pot;LocalTime;Soil;WaterLevel;Disease;Modifier;RainBeforeNextWindowMm;Recommended;RecommendedAmountMl;AutoAllowed;WaitingForRain;Status;Warning;WeatherImpact;DiseaseImpact;Note");

        foreach (var x in results.OrderBy(x => x.LocalTime))
        {
            writer.WriteLine(string.Join(';',
                Csv(x.Id),
                x.PotId,
                Csv(x.PotName),
                Csv(x.LocalTime.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture)),
                x.Soil.ToString("0.00", CultureInfo.InvariantCulture),
                x.WaterLevel.ToString("0.00", CultureInfo.InvariantCulture),
                Csv(x.Disease),
                x.DiseaseModifier.ToString("0.00", CultureInfo.InvariantCulture),
                x.RainBeforeNextWindowMm.ToString("0.0", CultureInfo.InvariantCulture),
                x.Recommended,
                x.RecommendedAmountMl,
                x.AutoAllowed,
                x.WaitingForRain,
                Csv(x.Status),
                Csv(x.Warning),
                Csv(x.WeatherImpact),
                Csv(x.DiseaseImpact),
                Csv(x.Note)));
        }

        Console.WriteLine($"Matrica stvarnih rezultata enginea: {path}");
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters) cmd.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarStringAsync(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters) cmd.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return (await cmd.ExecuteScalarAsync())?.ToString();
    }

    private static SeedMode ParseMode(string[] args)
    {
        if (args.Any(x => x.Equals("--reset", StringComparison.OrdinalIgnoreCase))) return SeedMode.Reset;
        if (args.Any(x => x.Equals("--reseed", StringComparison.OrdinalIgnoreCase))) return SeedMode.Reseed;
        return SeedMode.Seed;
    }

    private static string ResolveConnectionString(string[] args)
    {
        var inline = args.FirstOrDefault(x => x.StartsWith("--connection=", StringComparison.OrdinalIgnoreCase));
        if (inline is not null) return inline[(inline.IndexOf('=') + 1)..].Trim('"');

        var env = Environment.GetEnvironmentVariable("BASHTA_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        var appSettings = FindAppSettings(Directory.GetCurrentDirectory());
        if (appSettings is not null)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(appSettings));
            if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings) &&
                connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection))
            {
                var value = defaultConnection.GetString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }

        throw new InvalidOperationException(
            "Connection string nije pronađen. Pokreni iz root foldera bashta_be gdje postoji Bashta.API/appsettings.json " +
            "ili postavi BASHTA_DB_CONNECTION environment varijablu.");
    }

    private static string? FindAppSettings(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Bashta.API", "appsettings.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        return null;
    }

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.FromHours(2));

    private static DateTime ToDbTimestamp(DateTime utcValue, TimestampKind kind) =>
        kind == TimestampKind.WithTimeZone
            ? DateTime.SpecifyKind(utcValue, DateTimeKind.Utc)
            : DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcValue, DateTimeKind.Utc), SarajevoTimeZone), DateTimeKind.Unspecified);

    private static TimeZoneInfo SarajevoTimeZone =>
        TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Central European Standard Time" : "Europe/Sarajevo");

    private static decimal Round1(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal Clamp(decimal value, decimal min, decimal max) => Math.Min(max, Math.Max(min, value));
    private static double NextNoise(Random random, double amplitude) => (random.NextDouble() * 2.0 - 1.0) * amplitude;

    private enum SeedMode { Seed, Reset, Reseed }
    private enum TimestampKind { WithTimeZone, WithoutTimeZone }

    private sealed class PotState(decimal soil, decimal water)
    {
        public decimal Soil { get; set; } = soil;
        public decimal Water { get; set; } = water;
    }

    private sealed record PotInfo(int Role, int Id, string Name, bool IsRainExposed, int IntervalMinutes, int PlantId);
    private sealed record PlantTypeInfo(int Id, string Name, string DisplayName, int MinSoilMoisture, int MaxSoilMoisture);
    private sealed record DiseaseInfo(int Id, string Name, string DisplayName, decimal Modifier);
    private sealed record DiseaseContext(string? Name, decimal Modifier);
    private sealed record Ambient(decimal Temperature, decimal Humidity, int Lux);
    private sealed record RainEvent(DateOnly Date, decimal AmountMm, TimeOnly LocalTime);

    private sealed record WeatherContext(
        bool RainExpectedIn24h,
        decimal RainAmountNext24hMm,
        bool RainExpectedBeforeNextWindow,
        decimal RainAmountBeforeNextWindowMm,
        string RainIntensity,
        decimal MaxTemperatureNext24h,
        bool HeatRiskNext24h,
        string Summary);

    private sealed record ScenarioDefinition(
        string Id,
        int PotRole,
        DateTimeOffset LocalTime,
        decimal SoilMoisture,
        decimal? WaterLevel,
        decimal Temperature,
        decimal Humidity,
        int Lux,
        decimal? RainBeforeNextWindowMm,
        string Note);

    private sealed record ScenarioResult(
        string Id,
        int PotId,
        string PotName,
        DateTimeOffset LocalTime,
        decimal Soil,
        decimal WaterLevel,
        string Disease,
        decimal DiseaseModifier,
        decimal RainBeforeNextWindowMm,
        bool Recommended,
        int RecommendedAmountMl,
        bool AutoAllowed,
        bool WaitingForRain,
        string Status,
        string? Warning,
        string? WeatherImpact,
        string? DiseaseImpact,
        string Note);
}