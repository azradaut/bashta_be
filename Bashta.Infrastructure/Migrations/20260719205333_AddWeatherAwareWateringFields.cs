using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bashta.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeatherAwareWateringFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
        ALTER TABLE plant_pot
        ADD COLUMN IF NOT EXISTS is_rain_exposed boolean NOT NULL DEFAULT false;
    """);

            migrationBuilder.Sql("""
        ALTER TABLE plant_pot
        ADD COLUMN IF NOT EXISTS sensor_reading_interval_minutes integer NOT NULL DEFAULT 60;
    """);

            migrationBuilder.Sql("""
        ALTER TABLE watering_event
        ADD COLUMN IF NOT EXISTS is_forced boolean NOT NULL DEFAULT false;
    """);

            migrationBuilder.Sql("""
        ALTER TABLE watering_event
        ADD COLUMN IF NOT EXISTS decision_reason text;
    """);

            migrationBuilder.Sql("""
        ALTER TABLE watering_event
        ADD COLUMN IF NOT EXISTS weather_summary text;
    """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
        ALTER TABLE watering_event
        DROP COLUMN IF EXISTS weather_summary;
    """);

            migrationBuilder.Sql("""
        ALTER TABLE watering_event
        DROP COLUMN IF EXISTS decision_reason;
    """);

            migrationBuilder.Sql("""
        ALTER TABLE watering_event
        DROP COLUMN IF EXISTS is_forced;
    """);

            migrationBuilder.Sql("""
        ALTER TABLE plant_pot
        DROP COLUMN IF EXISTS sensor_reading_interval_minutes;
    """);

            migrationBuilder.Sql("""
        ALTER TABLE plant_pot
        DROP COLUMN IF EXISTS is_rain_exposed;
    """);
        }
    }
}
