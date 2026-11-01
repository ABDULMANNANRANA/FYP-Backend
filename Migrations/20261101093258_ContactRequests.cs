using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TODOLISTAPI.Migrations
{
    /// <inheritdoc />
    public partial class ContactRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContactUserId",
                table: "PersonalContacts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContactRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LowerUserId = table.Column<int>(type: "int", nullable: false),
                    UpperUserId = table.Column<int>(type: "int", nullable: false),
                    SenderUserId = table.Column<int>(type: "int", nullable: false),
                    RecipientUserId = table.Column<int>(type: "int", nullable: false),
                    SenderAlias = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactRequests", x => x.Id);
                    table.CheckConstraint("CK_ContactRequests_Pair", "[LowerUserId] < [UpperUserId] AND (([SenderUserId] = [LowerUserId] AND [RecipientUserId] = [UpperUserId]) OR ([SenderUserId] = [UpperUserId] AND [RecipientUserId] = [LowerUserId]))");
                    table.CheckConstraint("CK_ContactRequests_Status", "[Status] IN ('Pending','Accepted','Rejected','Cancelled','Removed')");
                    table.ForeignKey(
                        name: "FK_ContactRequests_Users_LowerUserId",
                        column: x => x.LowerUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContactRequests_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContactRequests_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContactRequests_Users_UpperUserId",
                        column: x => x.UpperUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalContacts_ContactUserId",
                table: "PersonalContacts",
                column: "ContactUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalContacts_UserId_ContactUserId",
                table: "PersonalContacts",
                columns: new[] { "UserId", "ContactUserId" },
                unique: true,
                filter: "[ContactUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactRequests_LowerUserId_UpperUserId",
                table: "ContactRequests",
                columns: new[] { "LowerUserId", "UpperUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContactRequests_RecipientUserId_Status",
                table: "ContactRequests",
                columns: new[] { "RecipientUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactRequests_SenderUserId_Status",
                table: "ContactRequests",
                columns: new[] { "SenderUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactRequests_UpperUserId",
                table: "ContactRequests",
                column: "UpperUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PersonalContacts_Users_ContactUserId",
                table: "PersonalContacts",
                column: "ContactUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PersonalContacts_Users_ContactUserId",
                table: "PersonalContacts");

            migrationBuilder.DropTable(
                name: "ContactRequests");

            migrationBuilder.DropIndex(
                name: "IX_PersonalContacts_ContactUserId",
                table: "PersonalContacts");

            migrationBuilder.DropIndex(
                name: "IX_PersonalContacts_UserId_ContactUserId",
                table: "PersonalContacts");

            migrationBuilder.DropColumn(
                name: "ContactUserId",
                table: "PersonalContacts");
        }
    }
}
