using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261107120000_TenantPhoneRegistration")]
public sealed class TenantPhoneRegistration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_TenantProfiles_PhoneNumber", schema: "dbo",
            table: "TenantProfiles", column: "PhoneNumber");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TenantProfiles_PhoneNumber", schema: "dbo", table: "TenantProfiles");
    }
}
