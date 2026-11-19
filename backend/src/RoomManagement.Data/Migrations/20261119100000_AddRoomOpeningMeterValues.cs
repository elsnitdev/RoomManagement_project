using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261119100000_AddRoomOpeningMeterValues")]
public sealed class AddRoomOpeningMeterValues : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>("OpeningElectricity", schema: "dbo",
            table: "Rooms", type: "decimal(18,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>("OpeningWater", schema: "dbo",
            table: "Rooms", type: "decimal(18,2)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("OpeningElectricity", schema: "dbo", table: "Rooms");
        migrationBuilder.DropColumn("OpeningWater", schema: "dbo", table: "Rooms");
    }
}
