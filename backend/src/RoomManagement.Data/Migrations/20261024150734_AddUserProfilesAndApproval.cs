using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfilesAndApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(300)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(20)",
                nullable: false,
                defaultValueSql: "N'Approved'")
                .Annotation("Relational:DefaultConstraintName", "DF_Users_ApprovalStatus");

            migrationBuilder.AddColumn<string>(
                name: "CitizenId",
                schema: "dbo",
                table: "Users",
                type: "varchar(20)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                schema: "dbo",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(150)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "dbo",
                table: "Users",
                type: "varchar(20)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(500)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                schema: "dbo",
                table: "Users",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Users_CitizenId",
                schema: "dbo",
                table: "Users",
                column: "CitizenId",
                unique: true,
                filter: "CitizenId IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_ApprovalStatus",
                schema: "dbo",
                table: "Users",
                sql: "(ApprovalStatus IN (N'Pending', N'Approved', N'Rejected'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Users_CitizenId",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_ApprovalStatus",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Address",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                schema: "dbo",
                table: "Users")
                .Annotation("Relational:DefaultConstraintName", "DF_Users_ApprovalStatus");

            migrationBuilder.DropColumn(
                name: "CitizenId",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FullName",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                schema: "dbo",
                table: "Users");
        }
    }
}
