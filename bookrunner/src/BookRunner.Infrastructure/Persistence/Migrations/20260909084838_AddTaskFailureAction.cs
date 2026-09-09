using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookRunner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskFailureAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailureAction",
                schema: "bookrunner",
                table: "Tasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FailureScenarioGroup",
                schema: "bookrunner",
                table: "Tasks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureAction",
                schema: "bookrunner",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "FailureScenarioGroup",
                schema: "bookrunner",
                table: "Tasks");
        }
    }
}
