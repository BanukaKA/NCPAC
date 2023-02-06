using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCPAC_LambdaX.Data.NCPACMigrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Commitees",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommiteeName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Division = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commitees", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MailPrefferences",
                columns: table => new
                {
                    ID = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailPrefferences", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Provinces",
                columns: table => new
                {
                    ID = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    MiddleName = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Salutation = table.Column<string>(type: "TEXT", nullable: true),
                    StreetAddress = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ProvinceID = table.Column<string>(type: "TEXT", nullable: false),
                    PostalCode = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    WorkStreetAddress = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    WorkCity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    WorkProvinceID = table.Column<string>(type: "TEXT", nullable: true),
                    WorkPostalCode = table.Column<string>(type: "TEXT", maxLength: 6, nullable: true),
                    WorkPhone = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    WorkEmail = table.Column<string>(type: "TEXT", nullable: true),
                    MailPrefferenceID = table.Column<string>(type: "TEXT", nullable: true),
                    EducationalSummary = table.Column<string>(type: "TEXT", nullable: true),
                    IsNCGrad = table.Column<bool>(type: "INTEGER", nullable: false),
                    OccupationalSummary = table.Column<string>(type: "TEXT", nullable: true),
                    DateJoined = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Members_MailPrefferences_MailPrefferenceID",
                        column: x => x.MailPrefferenceID,
                        principalTable: "MailPrefferences",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Members_Provinces_ProvinceID",
                        column: x => x.ProvinceID,
                        principalTable: "Provinces",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Members_Provinces_WorkProvinceID",
                        column: x => x.WorkProvinceID,
                        principalTable: "Provinces",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MemberCommitees",
                columns: table => new
                {
                    MemberID = table.Column<int>(type: "INTEGER", nullable: false),
                    CommiteeID = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberCommitees", x => new { x.MemberID, x.CommiteeID });
                    table.ForeignKey(
                        name: "FK_MemberCommitees_Commitees_CommiteeID",
                        column: x => x.CommiteeID,
                        principalTable: "Commitees",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberCommitees_Members_MemberID",
                        column: x => x.MemberID,
                        principalTable: "Members",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberCommitees_CommiteeID",
                table: "MemberCommitees",
                column: "CommiteeID");

            migrationBuilder.CreateIndex(
                name: "IX_Members_MailPrefferenceID",
                table: "Members",
                column: "MailPrefferenceID");

            migrationBuilder.CreateIndex(
                name: "IX_Members_ProvinceID",
                table: "Members",
                column: "ProvinceID");

            migrationBuilder.CreateIndex(
                name: "IX_Members_WorkProvinceID",
                table: "Members",
                column: "WorkProvinceID");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberCommitees");

            migrationBuilder.DropTable(
                name: "Commitees");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "MailPrefferences");

            migrationBuilder.DropTable(
                name: "Provinces");
        }
    }
}
