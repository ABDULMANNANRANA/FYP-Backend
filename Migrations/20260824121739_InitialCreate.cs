using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TODOLISTAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Password = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    IsActive = table.Column<bool>(type: "bit", nullable: true, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Users__3214EC070A53C32D", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupsUser",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__GroupsUs__3214EC0767F16719", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupsUser_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    NotificationEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DarkModeEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SnoozeMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__UserSett__3214EC070E164229", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSettings_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Role = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "Member"),
                    AddedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__GroupMem__3214EC0784E9E556", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupMembers_Group",
                        column: x => x.GroupId,
                        principalTable: "GroupsUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupMembers_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    IsTimeBased = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    AssignedTo = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Tasks__3214EC07FA8CA435", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasks_AssignedTo",
                        column: x => x.AssignedTo,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tasks_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tasks_Group",
                        column: x => x.GroupId,
                        principalTable: "GroupsUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ForwardedTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalTaskId = table.Column<int>(type: "int", nullable: false),
                    ForwardedBy = table.Column<int>(type: "int", nullable: false),
                    ForwardedTo = table.Column<int>(type: "int", nullable: false),
                    ForwardedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Forwarde__3214EC075A61EDB1", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Forwarded_By",
                        column: x => x.ForwardedBy,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Forwarded_Task",
                        column: x => x.OriginalTaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Forwarded_To",
                        column: x => x.ForwardedTo,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Notifica__3214EC07EB8250D3", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Task",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SnoozeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SnoozedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    WakeAt = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SnoozeLo__3214EC07D8C584B4", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Snooze_Task",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Snooze_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForwardedTasks_ForwardedBy",
                table: "ForwardedTasks",
                column: "ForwardedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardedTasks_ForwardedTo",
                table: "ForwardedTasks",
                column: "ForwardedTo");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardedTasks_OriginalTaskId",
                table: "ForwardedTasks",
                column: "OriginalTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_GroupId",
                table: "GroupMembers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_UserId",
                table: "GroupMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupsUser_CreatedBy",
                table: "GroupsUser",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TaskId",
                table: "Notifications",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SnoozeLogs_TaskId",
                table: "SnoozeLogs",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_SnoozeLogs_UserId",
                table: "SnoozeLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AssignedTo",
                table: "Tasks",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_CreatedBy",
                table: "Tasks",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_GroupId",
                table: "Tasks",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "UQ__Users__A9D10534AF9C5684",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__UserSett__1788CC4D7B6F48F8",
                table: "UserSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForwardedTasks");

            migrationBuilder.DropTable(
                name: "GroupMembers");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "SnoozeLogs");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "GroupsUser");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
