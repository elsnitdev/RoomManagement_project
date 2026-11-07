using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261107150000_LandlordScopedTenantOtp")]
public sealed class LandlordScopedTenantOtp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "LandlordCode", table: "Users", schema: "dbo", type: "varchar(10)", nullable: true);
        migrationBuilder.Sql("""
            WITH Landlords AS (
                SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) AS Number
                FROM dbo.Users WHERE Role = N'Landlord'
            )
            UPDATE u SET LandlordCode = 'L' + RIGHT('000000000' + CAST(l.Number AS varchar(9)), 9)
            FROM dbo.Users u JOIN Landlords l ON l.Id = u.Id;
            """);
        migrationBuilder.CreateIndex("UX_Users_LandlordCode", "Users", "LandlordCode", "dbo", unique: true,
            filter: "LandlordCode IS NOT NULL");
        migrationBuilder.Sql("""
            UPDATE u SET UserName = l.LandlordCode + ':' + p.PhoneNumber,
                         NormalizedUserName = l.LandlordCode + ':' + p.PhoneNumber
            FROM dbo.Users u
            JOIN dbo.TenantProfiles p ON p.UserId = u.Id
            JOIN dbo.Users l ON l.Id = p.CreatedByUserId
            WHERE u.Role = N'Tenant' AND p.PhoneNumber IS NOT NULL
              AND u.NormalizedUserName = p.PhoneNumber;
            """);
        migrationBuilder.CreateTable("TenantPhoneChallenges", schema: "dbo", columns: table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            LandlordId = table.Column<string>(type: "nvarchar(450)", nullable: false),
            PhoneNumber = table.Column<string>(type: "nvarchar(20)", nullable: false),
            CodeHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            Attempts = table.Column<int>(type: "int", nullable: false),
            VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_TenantPhoneChallenges", x => x.Id);
            table.ForeignKey("FK_TenantPhoneChallenges_Users_LandlordId", x => x.LandlordId, "Users", "Id", "dbo");
        });
        migrationBuilder.CreateIndex("IX_TenantPhoneChallenges_LandlordId_PhoneNumber_CreatedAt",
            "TenantPhoneChallenges", new[] { "LandlordId", "PhoneNumber", "CreatedAt" }, "dbo");
        migrationBuilder.DropIndex("UX_TenantProfiles_CitizenId", "TenantProfiles", "dbo");
        migrationBuilder.CreateIndex("UX_TenantProfiles_Landlord_CitizenId", "TenantProfiles",
            new[] { "CreatedByUserId", "CitizenId" }, "dbo", unique: true,
            filter: "CreatedByUserId IS NOT NULL AND CitizenId IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("TenantPhoneChallenges", "dbo");
        migrationBuilder.DropIndex("UX_TenantProfiles_Landlord_CitizenId", "TenantProfiles", "dbo");
        migrationBuilder.CreateIndex("UX_TenantProfiles_CitizenId", "TenantProfiles", "CitizenId", "dbo", unique: true,
            filter: "CitizenId IS NOT NULL");
        migrationBuilder.DropIndex("UX_Users_LandlordCode", "Users", "dbo");
        migrationBuilder.DropColumn("LandlordCode", "Users", "dbo");
    }
}
