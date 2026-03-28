using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ReservationService.Migrations
{
    public partial class AddExamScheduleTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExamWeekEnd",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "ExamWeekStart",
                table: "StudentProfiles");

            migrationBuilder.CreateTable(
                name: "ExamSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Faculty = table.Column<string>(type: "text", nullable: false),
                    ExamWeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    ExamWeekEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamSchedules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamSchedules_Faculty",
                table: "ExamSchedules",
                column: "Faculty",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamSchedules");

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
        }
    }
}
