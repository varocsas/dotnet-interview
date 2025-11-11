using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncLogsAndState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TodoList",
                table: "TodoList");

            migrationBuilder.RenameTable(
                name: "TodoList",
                newName: "TodoLists");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "TodoLists",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TodoLists",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TodoLists",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_TodoLists",
                table: "TodoLists",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "SyncLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocalTodoListId = table.Column<long>(type: "bigint", nullable: true),
                    ExternalTodoListId = table.Column<long>(type: "bigint", nullable: true),
                    LocalTodoItemId = table.Column<long>(type: "bigint", nullable: true),
                    ExternalTodoItemId = table.Column<long>(type: "bigint", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TodoItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TodoListId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TodoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TodoItems_TodoLists_TodoListId",
                        column: x => x.TodoListId,
                        principalTable: "TodoLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TodoLists_UpdatedAt",
                table: "TodoLists",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_EntityType_EntityId",
                table: "SyncLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_Success_Timestamp",
                table: "SyncLogs",
                columns: new[] { "Success", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_Timestamp",
                table: "SyncLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_SyncStates_EntityType_ExternalTodoItemId",
                table: "SyncStates",
                columns: new[] { "EntityType", "ExternalTodoItemId" },
                unique: true,
                filter: "[ExternalTodoItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SyncStates_EntityType_ExternalTodoListId",
                table: "SyncStates",
                columns: new[] { "EntityType", "ExternalTodoListId" },
                unique: true,
                filter: "[ExternalTodoListId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SyncStates_EntityType_LocalTodoItemId",
                table: "SyncStates",
                columns: new[] { "EntityType", "LocalTodoItemId" },
                unique: true,
                filter: "[LocalTodoItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SyncStates_EntityType_LocalTodoListId",
                table: "SyncStates",
                columns: new[] { "EntityType", "LocalTodoListId" },
                unique: true,
                filter: "[LocalTodoListId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TodoItems_IsCompleted",
                table: "TodoItems",
                column: "IsCompleted");

            migrationBuilder.CreateIndex(
                name: "IX_TodoItems_TodoListId",
                table: "TodoItems",
                column: "TodoListId");

            migrationBuilder.CreateIndex(
                name: "IX_TodoItems_UpdatedAt",
                table: "TodoItems",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncLogs");

            migrationBuilder.DropTable(
                name: "SyncStates");

            migrationBuilder.DropTable(
                name: "TodoItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TodoLists",
                table: "TodoLists");

            migrationBuilder.DropIndex(
                name: "IX_TodoLists_UpdatedAt",
                table: "TodoLists");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TodoLists");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TodoLists");

            migrationBuilder.RenameTable(
                name: "TodoLists",
                newName: "TodoList");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "TodoList",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TodoList",
                table: "TodoList",
                column: "Id");
        }
    }
}
