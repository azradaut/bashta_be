using Bashta.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bashta.Infrastructure.Data;

public class BashtaDbContext : DbContext
{
    public BashtaDbContext(DbContextOptions<BashtaDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Disease> Diseases => Set<Disease>();
    public DbSet<PlantType> PlantTypes => Set<PlantType>();
    public DbSet<PlantPot> PlantPots => Set<PlantPot>();
    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<WateringEvent> WateringEvents => Set<WateringEvent>();
    public DbSet<DiseaseDetection> DiseaseDetections => Set<DiseaseDetection>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── TABELE ───────────────────────────────────────────
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Disease>().ToTable("disease");
        modelBuilder.Entity<PlantType>().ToTable("plant_type");
        modelBuilder.Entity<PlantPot>().ToTable("plant_pot");
        modelBuilder.Entity<Plant>().ToTable("plant");
        modelBuilder.Entity<WateringEvent>().ToTable("watering_event");
        modelBuilder.Entity<DiseaseDetection>().ToTable("disease_detection");
        modelBuilder.Entity<Recommendation>().ToTable("recommendation");
        modelBuilder.Entity<Notification>().ToTable("notification");
        modelBuilder.Entity<SensorReading>().ToTable("sensor_readings");

        // ── KOLONE (snake_case mapping) ───────────────────────

        modelBuilder.Entity<User>(e => {
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.Username).HasColumnName("username");
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.IsActive).HasColumnName("is_active");
        });

        modelBuilder.Entity<Disease>(e => {
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.NameLocal).HasColumnName("name_local");
            e.Property(x => x.WateringModifier).HasColumnName("watering_modifier");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.TreatmentRecommendation).HasColumnName("treatment_recommendation");
        });

        modelBuilder.Entity<PlantType>(e => {
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.NameLocal).HasColumnName("name_local");
            e.Property(x => x.MinSoilMoisture).HasColumnName("min_soil_moisture");
            e.Property(x => x.MaxSoilMoisture).HasColumnName("max_soil_moisture");
            e.Property(x => x.MinTemp).HasColumnName("min_temp");
            e.Property(x => x.MaxTemp).HasColumnName("max_temp");
            e.Property(x => x.MinHumidity).HasColumnName("min_humidity");
            e.Property(x => x.MaxHumidity).HasColumnName("max_humidity");
            e.Property(x => x.MinLux).HasColumnName("min_lux");
            e.Property(x => x.TargetDli).HasColumnName("target_dli");
            e.Property(x => x.WateringIntervalHours).HasColumnName("watering_interval_hours");
            e.Property(x => x.Notes).HasColumnName("notes");
        });

        modelBuilder.Entity<PlantPot>(e => {
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Location).HasColumnName("location");
            e.Property(x => x.MacAddress).HasColumnName("mac_address");
            e.Property(x => x.FirmwareVersion).HasColumnName("firmware_version");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasOne(x => x.User)
             .WithMany(x => x.PlantPots)
             .HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<Plant>(e => {
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PotId).HasColumnName("pot_id");
            e.Property(x => x.PlantTypeId).HasColumnName("plant_type_id");
            e.Property(x => x.Nickname).HasColumnName("nickname");
            e.Property(x => x.PlantedAt).HasColumnName("planted_at");
            e.Property(x => x.RemovedAt).HasColumnName("removed_at");
            e.Property(x => x.Notes).HasColumnName("notes");

            e.HasOne(x => x.PlantPot)
             .WithMany(x => x.Plants)
             .HasForeignKey(x => x.PotId);

            e.HasOne(x => x.PlantType)
             .WithMany(x => x.Plants)
             .HasForeignKey(x => x.PlantTypeId);
        });

        modelBuilder.Entity<WateringEvent>(e => {
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PotId).HasColumnName("pot_id");
            e.Property(x => x.TriggeredBy).HasColumnName("triggered_by");
            e.Property(x => x.DurationSec).HasColumnName("duration_sec");
            e.Property(x => x.AmountMl).HasColumnName("amount_ml");
            e.Property(x => x.SoilMoistureBefore).HasColumnName("soil_moisture_before");
            e.Property(x => x.SoilMoistureAfter).HasColumnName("soil_moisture_after");
            e.Property(x => x.Skipped).HasColumnName("skipped");
            e.Property(x => x.SkipReason).HasColumnName("skip_reason");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasOne(x => x.PlantPot)
             .WithMany(x => x.WateringEvents)
             .HasForeignKey(x => x.PotId);
        });

        modelBuilder.Entity<DiseaseDetection>(e => {
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PlantId).HasColumnName("plant_id");
            e.Property(x => x.DiseaseId).HasColumnName("disease_id");
            e.Property(x => x.Confidence).HasColumnName("confidence");
            e.Property(x => x.ImagePath).HasColumnName("image_path");
            e.Property(x => x.IsHealthy).HasColumnName("is_healthy");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasOne(x => x.Plant)
             .WithMany(x => x.DiseaseDetections)
             .HasForeignKey(x => x.PlantId);

            e.HasOne(x => x.Disease)
             .WithMany(x => x.DiseaseDetections)
             .HasForeignKey(x => x.DiseaseId);
        });

        modelBuilder.Entity<Recommendation>(e => {
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PlantId).HasColumnName("plant_id");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.Message).HasColumnName("message");
            e.Property(x => x.IsRead).HasColumnName("is_read");
            e.Property(x => x.Source).HasColumnName("source");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasOne(x => x.Plant)
             .WithMany(x => x.Recommendations)
             .HasForeignKey(x => x.PlantId);
        });

        modelBuilder.Entity<Notification>(e => {
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Body).HasColumnName("body");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.IsRead).HasColumnName("is_read");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasOne(x => x.User)
             .WithMany(x => x.Notifications)
             .HasForeignKey(x => x.UserId);
        });

        // ── SENSOR READINGS (TimescaleDB hypertable) ─────────
        modelBuilder.Entity<SensorReading>(e => {
            e.HasKey(x => new { x.Time, x.PotId });  // kompozitni primarni ključ

            e.Property(x => x.Time).HasColumnName("time");
            e.Property(x => x.PotId).HasColumnName("pot_id");
            e.Property(x => x.SoilMoisture).HasColumnName("soil_moisture");
            e.Property(x => x.Temperature).HasColumnName("temperature");
            e.Property(x => x.Humidity).HasColumnName("humidity");
            e.Property(x => x.Lux).HasColumnName("lux");

            e.HasOne(x => x.PlantPot)
             .WithMany(x => x.SensorReadings)
             .HasForeignKey(x => x.PotId);
        });
    }
}