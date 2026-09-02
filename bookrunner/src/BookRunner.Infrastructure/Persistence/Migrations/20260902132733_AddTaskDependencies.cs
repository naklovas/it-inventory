using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookRunner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Once yeni tabloyu olustur ki eski DependsOnTaskId kolonunu silmeden
            // once mevcut verileri oraya tasiyabilelim (tek onculden coklu oncule
            // veri kaybi olmadan gecis).
            migrationBuilder.CreateTable(
                name: "TaskDependencies",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DependsOnTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskDependencies_Tasks_DependsOnTaskId",
                        column: x => x.DependsOnTaskId,
                        principalSchema: "bookrunner",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskDependencies_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "bookrunner",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_DependsOnTaskId",
                schema: "bookrunner",
                table: "TaskDependencies",
                column: "DependsOnTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_TaskId_DependsOnTaskId",
                schema: "bookrunner",
                table: "TaskDependencies",
                columns: new[] { "TaskId", "DependsOnTaskId" },
                unique: true);

            // Mevcut tekli oncul degerlerini yeni tabloya tasi.
            migrationBuilder.Sql(
                """
                INSERT INTO [bookrunner].[TaskDependencies] ([Id], [TaskId], [DependsOnTaskId], [CreatedAt])
                SELECT NEWID(), [Id], [DependsOnTaskId], SYSDATETIMEOFFSET()
                FROM [bookrunner].[Tasks]
                WHERE [DependsOnTaskId] IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Tasks_DependsOnTaskId",
                schema: "bookrunner",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_DependsOnTaskId",
                schema: "bookrunner",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "DependsOnTaskId",
                schema: "bookrunner",
                table: "Tasks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskDependencies",
                schema: "bookrunner");

            migrationBuilder.AddColumn<Guid>(
                name: "DependsOnTaskId",
                schema: "bookrunner",
                table: "Tasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_DependsOnTaskId",
                schema: "bookrunner",
                table: "Tasks",
                column: "DependsOnTaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Tasks_DependsOnTaskId",
                schema: "bookrunner",
                table: "Tasks",
                column: "DependsOnTaskId",
                principalSchema: "bookrunner",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
