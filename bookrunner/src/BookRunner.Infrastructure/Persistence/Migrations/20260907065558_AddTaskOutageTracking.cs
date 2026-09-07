using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookRunner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskOutageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActualOutageMinutes",
                schema: "bookrunner",
                table: "Tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOutageStep",
                schema: "bookrunner",
                table: "Tasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PlannedOutageMinutes",
                schema: "bookrunner",
                table: "Tasks",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualOutageMinutes",
                schema: "bookrunner",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "IsOutageStep",
                schema: "bookrunner",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "PlannedOutageMinutes",
                schema: "bookrunner",
                table: "Tasks");
        }
    }
}
