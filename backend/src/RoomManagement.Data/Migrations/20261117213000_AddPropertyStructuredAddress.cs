using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261117213000_AddPropertyStructuredAddress")]
public sealed class AddPropertyStructuredAddress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "City",
            schema: "dbo",
            table: "Properties",
            type: "nvarchar(100)",
            nullable: false,
            defaultValueSql: "N''");

        migrationBuilder.AddColumn<string>(
            name: "Ward",
            schema: "dbo",
            table: "Properties",
            type: "nvarchar(100)",
            nullable: false,
            defaultValueSql: "N''");

        migrationBuilder.AddColumn<string>(
            name: "StreetAddress",
            schema: "dbo",
            table: "Properties",
            type: "nvarchar(200)",
            nullable: false,
            defaultValueSql: "N''");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "City",
            schema: "dbo",
            table: "Properties");

        migrationBuilder.DropColumn(
            name: "Ward",
            schema: "dbo",
            table: "Properties");

        migrationBuilder.DropColumn(
            name: "StreetAddress",
            schema: "dbo",
            table: "Properties");
    }
}
