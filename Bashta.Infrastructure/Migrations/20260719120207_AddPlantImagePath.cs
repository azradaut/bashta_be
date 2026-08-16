using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bashta.Infrastructure.Migrations
{
    public partial class AddPlantImagePath : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE plant
                ADD COLUMN IF NOT EXISTS image_path text;
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE plant
                DROP COLUMN IF EXISTS image_path;
            """);
        }
    }
}