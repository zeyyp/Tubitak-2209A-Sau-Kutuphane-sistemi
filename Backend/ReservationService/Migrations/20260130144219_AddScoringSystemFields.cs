using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservationService.Migrations
{
    public partial class AddScoringSystemFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PenaltyPoints",
                table: "StudentProfiles");

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "StudentProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExamWeekEnd",
                table: "StudentProfiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExamWeekStart",
                table: "StudentProfiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Reservations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "Reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Department",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "ExamWeekEnd",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "ExamWeekStart",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "Reservations");

            migrationBuilder.AddColumn<int>(
                name: "PenaltyPoints",
                table: "StudentProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
