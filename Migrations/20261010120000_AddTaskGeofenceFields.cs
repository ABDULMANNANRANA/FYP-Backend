using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TODOLISTAPI.Models;

#nullable disable

namespace TODOLISTAPI.Migrations
{
    [DbContext(typeof(TodoSmartAlertsContext))]
    [Migration("20261010120000_AddTaskGeofenceFields")]
    public partial class AddTaskGeofenceFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GeofenceEnabled",
                table: "Tasks",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "GeofenceRadiusMeters",
                table: "Tasks",
                type: "int",
                nullable: false,
                defaultValue: 200);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Tasks",
                type: "decimal(10, 7)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Tasks",
                type: "decimal(10, 7)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "GeofenceEnabled", table: "Tasks");
            migrationBuilder.DropColumn(name: "GeofenceRadiusMeters", table: "Tasks");
            migrationBuilder.DropColumn(name: "Latitude", table: "Tasks");
            migrationBuilder.DropColumn(name: "Longitude", table: "Tasks");
        }
    }
}
