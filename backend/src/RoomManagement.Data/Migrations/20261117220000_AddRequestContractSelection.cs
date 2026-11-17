using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261117220000_AddRequestContractSelection")]
public sealed class AddRequestContractSelection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ContractId", schema: "dbo", table: "Requests",
            type: "uniqueidentifier", nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Requests_ContractId", schema: "dbo", table: "Requests", column: "ContractId");

        migrationBuilder.AddForeignKey(
            name: "FK_Requests_Contracts", schema: "dbo", table: "Requests",
            column: "ContractId", principalSchema: "dbo", principalTable: "Contracts",
            principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Requests_Contracts", schema: "dbo", table: "Requests");
        migrationBuilder.DropIndex(name: "IX_Requests_ContractId", schema: "dbo", table: "Requests");
        migrationBuilder.DropColumn(name: "ContractId", schema: "dbo", table: "Requests");
    }
}
