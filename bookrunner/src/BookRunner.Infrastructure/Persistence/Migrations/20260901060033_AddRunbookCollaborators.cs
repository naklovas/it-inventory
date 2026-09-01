using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookRunner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunbookCollaborators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunbookCollaborators",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunbookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunbookCollaborators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunbookCollaborators_Runbooks_RunbookId",
                        column: x => x.RunbookId,
                        principalSchema: "bookrunner",
                        principalTable: "Runbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RunbookCollaborators_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "bookrunner",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunbookCollaborators_RunbookId_UserId",
                schema: "bookrunner",
                table: "RunbookCollaborators",
                columns: new[] { "RunbookId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunbookCollaborators_UserId",
                schema: "bookrunner",
                table: "RunbookCollaborators",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunbookCollaborators",
                schema: "bookrunner");
        }
    }
}
