using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookRunner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bookrunner");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RunbookId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailOutbox",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    To = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Cc = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RunbookId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Groups",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sid = table.Column<string>(type: "nvarchar(184)", maxLength: 184, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DistinguishedName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AvatarColor = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleMappings",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sid = table.Column<string>(type: "nvarchar(184)", maxLength: 184, nullable: false),
                    SamAccountName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserPrincipalName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Company = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OfficePhone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MobilePhone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ManagerDistinguishedName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DistinguishedName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Photo = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PhotoContentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PhotoHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Initials = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    AvatarColor = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Runbooks",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsTemplate = table.Column<bool>(type: "bit", nullable: false),
                    TemplateCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlannedStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PlannedEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActualStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActualEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceManagerWorkItemId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
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
                    table.PrimaryKey("PK_Runbooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Runbooks_Runbooks_SourceTemplateId",
                        column: x => x.SourceTemplateId,
                        principalSchema: "bookrunner",
                        principalTable: "Runbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Runbooks_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "bookrunner",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserGroups",
                schema: "bookrunner",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroups", x => new { x.UserId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_UserGroups_Groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "bookrunner",
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserGroups_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "bookrunner",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Scripts",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunbookId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scripts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scripts_Runbooks_RunbookId",
                        column: x => x.RunbookId,
                        principalSchema: "bookrunner",
                        principalTable: "Runbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunbookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    ColorHex = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "int", nullable: true),
                    PlannedStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PlannedEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActualStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActualEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DependsOnTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScriptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RollbackNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasks_Runbooks_RunbookId",
                        column: x => x.RunbookId,
                        principalSchema: "bookrunner",
                        principalTable: "Runbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tasks_Scripts_ScriptId",
                        column: x => x.ScriptId,
                        principalSchema: "bookrunner",
                        principalTable: "Scripts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasks_Tasks_DependsOnTaskId",
                        column: x => x.DependsOnTaskId,
                        principalSchema: "bookrunner",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScriptExecutions",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScriptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExecutedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Output = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScriptExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScriptExecutions_Scripts_ScriptId",
                        column: x => x.ScriptId,
                        principalSchema: "bookrunner",
                        principalTable: "Scripts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScriptExecutions_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "bookrunner",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScriptExecutions_Users_ExecutedByUserId",
                        column: x => x.ExecutedByUserId,
                        principalSchema: "bookrunner",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskActivities",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskActivities_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "bookrunner",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskActivities_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "bookrunner",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskAssignments",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssigneeType = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    HandedOverFromAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HandoverNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NotifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignments", x => x.Id);
                    table.CheckConstraint("CK_TaskAssignments_Target", "([AssigneeType] = 0 AND [UserId] IS NOT NULL AND [GroupId] IS NULL) OR ([AssigneeType] = 1 AND [GroupId] IS NOT NULL AND [UserId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_TaskAssignments_Groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "bookrunner",
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskAssignments_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "bookrunner",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "bookrunner",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskComments",
                schema: "bookrunner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ParentCommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MentionedUserIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsEdited = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TaskComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskComments_TaskComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalSchema: "bookrunner",
                        principalTable: "TaskComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskComments_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "bookrunner",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskComments_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalSchema: "bookrunner",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                schema: "bookrunner",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_RunbookId",
                schema: "bookrunner",
                table: "AuditLogs",
                column: "RunbookId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                schema: "bookrunner",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserName",
                schema: "bookrunner",
                table: "AuditLogs",
                column: "UserName");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_Status_NextAttemptAt",
                schema: "bookrunner",
                table: "EmailOutbox",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Name",
                schema: "bookrunner",
                table: "Groups",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Sid",
                schema: "bookrunner",
                table: "Groups",
                column: "Sid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleMappings_TeamName_Role",
                schema: "bookrunner",
                table: "RoleMappings",
                columns: new[] { "TeamName", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Runbooks_Code",
                schema: "bookrunner",
                table: "Runbooks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Runbooks_IsTemplate_Status",
                schema: "bookrunner",
                table: "Runbooks",
                columns: new[] { "IsTemplate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Runbooks_OwnerUserId",
                schema: "bookrunner",
                table: "Runbooks",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Runbooks_PlannedStart",
                schema: "bookrunner",
                table: "Runbooks",
                column: "PlannedStart");

            migrationBuilder.CreateIndex(
                name: "IX_Runbooks_ServiceManagerWorkItemId",
                schema: "bookrunner",
                table: "Runbooks",
                column: "ServiceManagerWorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Runbooks_SourceTemplateId",
                schema: "bookrunner",
                table: "Runbooks",
                column: "SourceTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptExecutions_ExecutedByUserId",
                schema: "bookrunner",
                table: "ScriptExecutions",
                column: "ExecutedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptExecutions_ScriptId",
                schema: "bookrunner",
                table: "ScriptExecutions",
                column: "ScriptId");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptExecutions_StartedAt",
                schema: "bookrunner",
                table: "ScriptExecutions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptExecutions_TaskId",
                schema: "bookrunner",
                table: "ScriptExecutions",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Scripts_Name",
                schema: "bookrunner",
                table: "Scripts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Scripts_RunbookId",
                schema: "bookrunner",
                table: "Scripts",
                column: "RunbookId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskActivities_ActorUserId",
                schema: "bookrunner",
                table: "TaskActivities",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskActivities_TaskId_CreatedAt",
                schema: "bookrunner",
                table: "TaskActivities",
                columns: new[] { "TaskId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_GroupId",
                schema: "bookrunner",
                table: "TaskAssignments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_TaskId_IsActive",
                schema: "bookrunner",
                table: "TaskAssignments",
                columns: new[] { "TaskId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_UserId",
                schema: "bookrunner",
                table: "TaskAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_AuthorUserId",
                schema: "bookrunner",
                table: "TaskComments",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_ParentCommentId",
                schema: "bookrunner",
                table: "TaskComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_TaskId_CreatedAt",
                schema: "bookrunner",
                table: "TaskComments",
                columns: new[] { "TaskId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_DependsOnTaskId",
                schema: "bookrunner",
                table: "Tasks",
                column: "DependsOnTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_RunbookId_Order",
                schema: "bookrunner",
                table: "Tasks",
                columns: new[] { "RunbookId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ScriptId",
                schema: "bookrunner",
                table: "Tasks",
                column: "ScriptId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Status",
                schema: "bookrunner",
                table: "Tasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroups_GroupId",
                schema: "bookrunner",
                table: "UserGroups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DisplayName",
                schema: "bookrunner",
                table: "Users",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SamAccountName",
                schema: "bookrunner",
                table: "Users",
                column: "SamAccountName");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Sid",
                schema: "bookrunner",
                table: "Users",
                column: "Sid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "EmailOutbox",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "RoleMappings",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "ScriptExecutions",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "TaskActivities",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "TaskAssignments",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "TaskComments",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "UserGroups",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "Tasks",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "Groups",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "Scripts",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "Runbooks",
                schema: "bookrunner");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "bookrunner");
        }
    }
}
