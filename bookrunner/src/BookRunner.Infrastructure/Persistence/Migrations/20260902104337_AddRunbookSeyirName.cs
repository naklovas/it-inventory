using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookRunner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunbookSeyirName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProgramName",
                schema: "bookrunner",
                table: "Runbooks",
                newName: "SeyirName");

            migrationBuilder.RenameIndex(
                name: "IX_Runbooks_ProgramName",
                schema: "bookrunner",
                table: "Runbooks",
                newName: "IX_Runbooks_SeyirName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SeyirName",
                schema: "bookrunner",
                table: "Runbooks",
                newName: "ProgramName");

            migrationBuilder.RenameIndex(
                name: "IX_Runbooks_SeyirName",
                schema: "bookrunner",
                table: "Runbooks",
                newName: "IX_Runbooks_ProgramName");
        }
    }
}
