using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261103160000_AddContractClosure")]
public sealed class AddContractClosure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "UX_Contracts_OneActivePerRoom", schema: "dbo", table: "Contracts");
        migrationBuilder.AddColumn<DateOnly>(name: "ClosedOn", schema: "dbo", table: "Contracts",
            type: "date", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ClosureReason", schema: "dbo", table: "Contracts",
            type: "nvarchar(1000)", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_Contracts_ActiveRoom", schema: "dbo", table: "Contracts",
            column: "RoomId", filter: "Status = N'Active'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Contracts_ActiveRoom", schema: "dbo", table: "Contracts");
        migrationBuilder.DropColumn(name: "ClosedOn", schema: "dbo", table: "Contracts");
        migrationBuilder.DropColumn(name: "ClosureReason", schema: "dbo", table: "Contracts");
        migrationBuilder.CreateIndex(name: "UX_Contracts_OneActivePerRoom", schema: "dbo", table: "Contracts",
            column: "RoomId", unique: true, filter: "Status = N'Active'");
    }
}
