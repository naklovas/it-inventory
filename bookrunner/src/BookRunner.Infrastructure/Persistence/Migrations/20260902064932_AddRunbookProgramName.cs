using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookRunner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunbookProgramName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProgramName",
                schema: "bookrunner",
                table: "Runbooks",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Runbooks_ProgramName",
                schema: "bookrunner",
                table: "Runbooks",
                column: "ProgramName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Runbooks_ProgramName",
                schema: "bookrunner",
                table: "Runbooks");

            migrationBuilder.DropColumn(
                name: "ProgramName",
                schema: "bookrunner",
                table: "Runbooks");
        }
    }
}
