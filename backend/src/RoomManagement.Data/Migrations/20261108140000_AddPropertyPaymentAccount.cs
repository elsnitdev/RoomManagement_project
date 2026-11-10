using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261108140000_AddPropertyPaymentAccount")]
public sealed class AddPropertyPaymentAccount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "BankName", schema: "dbo", table: "Properties", type: "nvarchar(150)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "BankAccountNumber", schema: "dbo", table: "Properties", type: "nvarchar(50)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "BankAccountHolder", schema: "dbo", table: "Properties", type: "nvarchar(150)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "BankName", schema: "dbo", table: "Properties");
        migrationBuilder.DropColumn(name: "BankAccountNumber", schema: "dbo", table: "Properties");
        migrationBuilder.DropColumn(name: "BankAccountHolder", schema: "dbo", table: "Properties");
    }
}
