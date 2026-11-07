using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261105120000_ContractHouseholdProfiles")]
public sealed class ContractHouseholdProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(name: "PhoneNumber", schema: "dbo", table: "TenantProfiles",
            type: "varchar(20)", nullable: true, oldClrType: typeof(string), oldType: "varchar(20)");
        migrationBuilder.AddColumn<bool>(name: "IsContactPerson", schema: "dbo", table: "ContractTenants",
            type: "bit", nullable: false, defaultValue: false);

        // Existing tenants had mandatory phone numbers. Pick the earliest present member as contact.
        migrationBuilder.Sql("""
            WITH Ranked AS (
                SELECT ct.Id, ROW_NUMBER() OVER (PARTITION BY ct.ContractId ORDER BY ct.JoinDate, ct.Id) AS rn
                FROM dbo.ContractTenants ct
                INNER JOIN dbo.TenantProfiles p ON p.Id = ct.TenantId
                WHERE ct.LeaveDate IS NULL AND
                    (NULLIF(LTRIM(RTRIM(p.PhoneNumber)), '') IS NOT NULL OR
                     NULLIF(LTRIM(RTRIM(p.Email)), '') IS NOT NULL)
            )
            UPDATE ct SET IsContactPerson = 1
            FROM dbo.ContractTenants ct INNER JOIN Ranked r ON r.Id = ct.Id
            WHERE r.rn = 1;
            """);
        migrationBuilder.CreateIndex(name: "UX_ContractTenants_ContactPerson", schema: "dbo",
            table: "ContractTenants", column: "ContractId", unique: true,
            filter: "IsContactPerson = 1 AND LeaveDate IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "UX_ContractTenants_ContactPerson", schema: "dbo", table: "ContractTenants");
        migrationBuilder.DropColumn(name: "IsContactPerson", schema: "dbo", table: "ContractTenants");
        migrationBuilder.AlterColumn<string>(name: "PhoneNumber", schema: "dbo", table: "TenantProfiles",
            type: "varchar(20)", nullable: false, defaultValue: "", oldClrType: typeof(string),
            oldType: "varchar(20)", oldNullable: true);
    }
}
