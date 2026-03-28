using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ReservationService.Migrations
{
    public partial class AddFacultyTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExamSchedules_Faculty",
                table: "ExamSchedules");

            migrationBuilder.DropColumn(
                name: "Faculty",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "Faculty",
                table: "ExamSchedules");

            migrationBuilder.AddColumn<int>(
                name: "FacultyId",
                table: "StudentProfiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FacultyId",
                table: "ExamSchedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Faculties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faculties", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_FacultyId",
                table: "StudentProfiles",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSchedules_FacultyId",
                table: "ExamSchedules",
                column: "FacultyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Faculties_Name",
                table: "Faculties",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamSchedules_Faculties_FacultyId",
                table: "ExamSchedules",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentProfiles_Faculties_FacultyId",
                table: "StudentProfiles",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamSchedules_Faculties_FacultyId",
                table: "ExamSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentProfiles_Faculties_FacultyId",
                table: "StudentProfiles");

            migrationBuilder.DropTable(
                name: "Faculties");

            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_FacultyId",
                table: "StudentProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ExamSchedules_FacultyId",
                table: "ExamSchedules");

            migrationBuilder.DropColumn(
                name: "FacultyId",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "FacultyId",
                table: "ExamSchedules");

            migrationBuilder.AddColumn<string>(
                name: "Faculty",
                table: "StudentProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Faculty",
                table: "ExamSchedules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSchedules_Faculty",
                table: "ExamSchedules",
                column: "Faculty",
                unique: true);
        }
    }
}
