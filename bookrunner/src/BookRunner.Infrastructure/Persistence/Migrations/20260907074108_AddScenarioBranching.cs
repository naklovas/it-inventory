using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookRunner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarioBranching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScenarioGroup",
                schema: "bookrunner",
                table: "Tasks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActiveScenarioGroup",
                schema: "bookrunner",
                table: "Runbooks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScenarioGroup",
                schema: "bookrunner",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ActiveScenarioGroup",
                schema: "bookrunner",
                table: "Runbooks");
        }
    }
}
