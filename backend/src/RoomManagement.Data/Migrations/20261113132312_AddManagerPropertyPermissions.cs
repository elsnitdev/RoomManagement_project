using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerPropertyPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Role",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UX_TenantProfiles_Landlord_CitizenId",
                schema: "dbo",
                table: "TenantProfiles");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "dbo",
                table: "Users",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LandlordUserId",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValueSql: "0")
                .Annotation("Relational:DefaultConstraintName", "DF_Users_MustChangePassword");

            migrationBuilder.AddColumn<string>(
                name: "OwnerLandlordUserId",
                schema: "dbo",
                table: "TenantProfiles",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE tp
                SET tp.OwnerLandlordUserId = CASE
                    WHEN u.Role = N'Manager' THEN u.LandlordUserId
                    ELSE tp.CreatedByUserId
                END
                FROM dbo.TenantProfiles tp
                INNER JOIN dbo.Users u ON u.Id = tp.CreatedByUserId
                WHERE tp.OwnerLandlordUserId IS NULL
                  AND tp.CreatedByUserId IS NOT NULL;

                UPDATE tp
                SET tp.OwnerLandlordUserId = src.OwnerUserId
                FROM dbo.TenantProfiles tp
                CROSS APPLY (
                    SELECT TOP (1) p.OwnerUserId
                    FROM dbo.ContractTenants ct
                    INNER JOIN dbo.Contracts c ON c.Id = ct.ContractId
                    INNER JOIN dbo.Rooms r ON r.Id = c.RoomId
                    INNER JOIN dbo.Properties p ON p.Id = r.PropertyId
                    WHERE ct.TenantId = tp.Id
                    ORDER BY ct.CreatedAt ASC, ct.Id ASC
                ) src
                WHERE tp.OwnerLandlordUserId IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "PropertyManagerAssignments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PropertyManagerAssignments_AssignedAt"),
                    RevokedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RevokedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyManagerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyManagerAssignments_AssignedBy",
                        column: x => x.AssignedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PropertyManagerAssignments_ManagerUser",
                        column: x => x.ManagerUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PropertyManagerAssignments_Property",
                        column: x => x.PropertyId,
                        principalSchema: "dbo",
                        principalTable: "Properties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PropertyManagerAssignments_RevokedBy",
                        column: x => x.RevokedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_LandlordUserId",
                schema: "dbo",
                table: "Users",
                column: "LandlordUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_ManagerLandlord",
                schema: "dbo",
                table: "Users",
                sql: "((Role = N'Manager' AND LandlordUserId IS NOT NULL) OR (Role <> N'Manager' AND LandlordUserId IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Role",
                schema: "dbo",
                table: "Users",
                sql: "(Role IN (N'Admin', N'Landlord', N'Manager', N'Tenant'))");

            migrationBuilder.CreateIndex(
                name: "IX_TenantProfiles_OwnerLandlordUserId",
                schema: "dbo",
                table: "TenantProfiles",
                column: "OwnerLandlordUserId");

            migrationBuilder.CreateIndex(
                name: "UX_TenantProfiles_Landlord_CitizenId",
                schema: "dbo",
                table: "TenantProfiles",
                columns: new[] { "OwnerLandlordUserId", "CitizenId" },
                unique: true,
                filter: "OwnerLandlordUserId IS NOT NULL AND CitizenId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyManagerAssignments_AssignedByUserId",
                schema: "dbo",
                table: "PropertyManagerAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyManagerAssignments_PropertyId",
                schema: "dbo",
                table: "PropertyManagerAssignments",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyManagerAssignments_RevokedByUserId",
                schema: "dbo",
                table: "PropertyManagerAssignments",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_PropertyManagerAssignments_Active",
                schema: "dbo",
                table: "PropertyManagerAssignments",
                columns: new[] { "ManagerUserId", "PropertyId" },
                unique: true,
                filter: "RevokedAt IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantProfiles_OwnerLandlordUser",
                schema: "dbo",
                table: "TenantProfiles",
                column: "OwnerLandlordUserId",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_LandlordUser",
                schema: "dbo",
                table: "Users",
                column: "LandlordUserId",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TenantProfiles_OwnerLandlordUser",
                schema: "dbo",
                table: "TenantProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_LandlordUser",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropTable(
                name: "PropertyManagerAssignments",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Users_LandlordUserId",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_ManagerLandlord",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Role",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TenantProfiles_OwnerLandlordUserId",
                schema: "dbo",
                table: "TenantProfiles");

            migrationBuilder.DropIndex(
                name: "UX_TenantProfiles_Landlord_CitizenId",
                schema: "dbo",
                table: "TenantProfiles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LandlordUserId",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                schema: "dbo",
                table: "Users")
                .Annotation("Relational:DefaultConstraintName", "DF_Users_MustChangePassword");

            migrationBuilder.DropColumn(
                name: "OwnerLandlordUserId",
                schema: "dbo",
                table: "TenantProfiles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Role",
                schema: "dbo",
                table: "Users",
                sql: "(Role IN (N'Admin', N'Landlord', N'Tenant'))");

            migrationBuilder.CreateIndex(
                name: "UX_TenantProfiles_Landlord_CitizenId",
                schema: "dbo",
                table: "TenantProfiles",
                columns: new[] { "CreatedByUserId", "CitizenId" },
                unique: true,
                filter: "CreatedByUserId IS NOT NULL AND CitizenId IS NOT NULL");
        }
    }
}
