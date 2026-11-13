using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261113120000_ContractTenancyModes")]
public sealed class ContractTenancyModes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "TenancyMode", schema: "dbo", table: "Contracts",
            type: "nvarchar(32)", nullable: false, defaultValue: "CoTenancy");
        migrationBuilder.AddColumn<string>(name: "Role", schema: "dbo", table: "ContractTenants",
            type: "nvarchar(32)", nullable: false, defaultValue: "ContractParty");
        migrationBuilder.AddColumn<DateTime>(name: "ResidenceApprovedAt", schema: "dbo",
            table: "ContractTenants", type: "datetime2(0)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ResidenceApprovedByUserId", schema: "dbo",
            table: "ContractTenants", type: "nvarchar(450)", nullable: true);
        migrationBuilder.AddCheckConstraint(name: "CK_Contracts_TenancyMode", schema: "dbo",
            table: "Contracts", sql: "TenancyMode IN (N'CoTenancy', N'PrimaryTenantWithFamily')");
        migrationBuilder.AddCheckConstraint(name: "CK_ContractTenants_Role", schema: "dbo",
            table: "ContractTenants", sql: "Role IN (N'ContractParty', N'AuthorizedOccupant')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_ContractTenants_Role", schema: "dbo", table: "ContractTenants");
        migrationBuilder.DropCheckConstraint(name: "CK_Contracts_TenancyMode", schema: "dbo", table: "Contracts");
        migrationBuilder.DropColumn(name: "ResidenceApprovedAt", schema: "dbo", table: "ContractTenants");
        migrationBuilder.DropColumn(name: "ResidenceApprovedByUserId", schema: "dbo", table: "ContractTenants");
        migrationBuilder.DropColumn(name: "Role", schema: "dbo", table: "ContractTenants");
        migrationBuilder.DropColumn(name: "TenancyMode", schema: "dbo", table: "Contracts");
    }
}
