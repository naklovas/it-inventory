using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookRunner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarioEntityAndFailureTargetTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FailureTargetTaskId",
                schema: "bookrunner",
                table: "Tasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Scenarios",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunbookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RejoinTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scenarios_Runbooks_RunbookId",
                        column: x => x.RunbookId,
                        principalSchema: "bookrunner",
                        principalTable: "Runbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Scenarios_Tasks_RejoinTaskId",
                        column: x => x.RejoinTaskId,
                        principalSchema: "bookrunner",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_RejoinTaskId",
                schema: "bookrunner",
                table: "Scenarios",
                column: "RejoinTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_RunbookId_Name",
                schema: "bookrunner",
                table: "Scenarios",
                columns: new[] { "RunbookId", "Name" },
                unique: true);

            // Geriye donuk doldurma: bu ozellik eklenmeden once bir senaryo,
            // yalnizca gorevlerin ScenarioGroup metniyle "var" sayiliyordu - ayri
            // bir Scenario satiri yoktu. Mevcut runbook'larda halihazirda
            // kullanilan her (RunbookId, ScenarioGroup) cifti icin bir Scenario
            // satiri olusturulur, aksi halde bu runbook'larda "Senaryo Adimi Ekle"
            // formu (artik yalnizca var olan senaryolardan secim yaptirdigi icin)
            // hicbir secenek gostermezdi.
            migrationBuilder.Sql(@"
                INSERT INTO [bookrunner].[Scenarios] ([Id], [RunbookId], [Name], [RejoinTaskId], [IsDeleted], [CreatedAt], [CreatedBy])
                SELECT NEWID(), x.RunbookId, x.ScenarioGroup, x.RejoinTaskId, 0, SYSDATETIMEOFFSET(), N'system-backfill'
                FROM (
                    SELECT DISTINCT t.RunbookId, t.ScenarioGroup,
                        (SELECT TOP 1 t2.ScenarioRejoinTaskId FROM [bookrunner].[Tasks] t2
                         WHERE t2.RunbookId = t.RunbookId AND t2.ScenarioGroup = t.ScenarioGroup
                            AND t2.ScenarioRejoinTaskId IS NOT NULL) AS RejoinTaskId
                    FROM [bookrunner].[Tasks] t
                    WHERE t.ScenarioGroup IS NOT NULL AND t.IsRollbackStep = 0 AND t.IsDeleted = 0
                ) AS x
                WHERE NOT EXISTS (
                    SELECT 1 FROM [bookrunner].[Scenarios] s
                    WHERE s.RunbookId = x.RunbookId AND s.Name = x.ScenarioGroup
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Scenarios",
                schema: "bookrunner");

            migrationBuilder.DropColumn(
                name: "FailureTargetTaskId",
                schema: "bookrunner",
                table: "Tasks");
        }
    }
}
