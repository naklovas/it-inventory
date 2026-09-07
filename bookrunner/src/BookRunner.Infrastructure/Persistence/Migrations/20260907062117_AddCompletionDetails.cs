using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookRunner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActualMinutes",
                schema: "bookrunner",
                table: "Tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionNote",
                schema: "bookrunner",
                table: "Tasks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActualMinutes",
                schema: "bookrunner",
                table: "Runbooks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionNote",
                schema: "bookrunner",
                table: "Runbooks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualMinutes",
                schema: "bookrunner",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "CompletionNote",
                schema: "bookrunner",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ActualMinutes",
                schema: "bookrunner",
                table: "Runbooks");

            migrationBuilder.DropColumn(
                name: "CompletionNote",
                schema: "bookrunner",
                table: "Runbooks");
        }
    }
}
