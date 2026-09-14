using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookRunner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunbookRollbackAutoTriggered : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRollbackAutoTriggered",
                schema: "bookrunner",
                table: "Runbooks",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRollbackAutoTriggered",
                schema: "bookrunner",
                table: "Runbooks");
        }
    }
}
