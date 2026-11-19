using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261119090000_AddContractOfferResponseDeadline")]
public sealed class AddContractOfferResponseDeadline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>("OfferResponseDeadline", schema: "dbo",
            table: "Contracts", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>("OfferResponseTimedOutAt", schema: "dbo",
            table: "Contracts", type: "datetime2", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("OfferResponseDeadline", schema: "dbo", table: "Contracts");
        migrationBuilder.DropColumn("OfferResponseTimedOutAt", schema: "dbo", table: "Contracts");
    }
}
