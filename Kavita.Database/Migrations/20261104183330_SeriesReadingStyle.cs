using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class SeriesReadingStyle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "ManhwaSpeedMultiplier",
                table: "Series",
                type: "REAL",
                nullable: false,
                defaultValue: 2f);

            migrationBuilder.AddColumn<int>(
                name: "ReadingStyle",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManhwaSpeedMultiplier",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "ReadingStyle",
                table: "Series");
        }
    }
}
