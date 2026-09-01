using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookRunner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppGroupIsTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTeam",
                schema: "bookrunner",
                table: "Groups",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTeam",
                schema: "bookrunner",
                table: "Groups");
        }
    }
}
