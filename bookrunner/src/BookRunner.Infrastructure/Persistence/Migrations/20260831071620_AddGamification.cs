using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookRunner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGamification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TeamName",
                schema: "bookrunner",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Badges",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Badges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GamificationEvents",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    RunbookId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RunbookTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GamificationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GamificationEvents_Runbooks_RunbookId",
                        column: x => x.RunbookId,
                        principalSchema: "bookrunner",
                        principalTable: "Runbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GamificationEvents_Tasks_RunbookTaskId",
                        column: x => x.RunbookTaskId,
                        principalSchema: "bookrunner",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GamificationEvents_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "bookrunner",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserBadges",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BadgeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EarnedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBadges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBadges_Badges_BadgeId",
                        column: x => x.BadgeId,
                        principalSchema: "bookrunner",
                        principalTable: "Badges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBadges_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "bookrunner",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Badges_Code",
                schema: "bookrunner",
                table: "Badges",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GamificationEvents_CreatedAt",
                schema: "bookrunner",
                table: "GamificationEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GamificationEvents_RunbookId",
                schema: "bookrunner",
                table: "GamificationEvents",
                column: "RunbookId");

            migrationBuilder.CreateIndex(
                name: "IX_GamificationEvents_RunbookTaskId",
                schema: "bookrunner",
                table: "GamificationEvents",
                column: "RunbookTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_GamificationEvents_UserId_EventType",
                schema: "bookrunner",
                table: "GamificationEvents",
                columns: new[] { "UserId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_UserBadges_BadgeId",
                schema: "bookrunner",
                table: "UserBadges",
                column: "BadgeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadges_UserId_BadgeId",
                schema: "bookrunner",
                table: "UserBadges",
                columns: new[] { "UserId", "BadgeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GamificationEvents",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "UserBadges",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "Badges",
                schema: "bookrunner");

            migrationBuilder.DropColumn(
                name: "TeamName",
                schema: "bookrunner",
                table: "Users");
        }
    }
}
