using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class PropertyServicesAndQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Services_Name",
                schema: "dbo",
                table: "Services");

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                schema: "dbo",
                table: "Services",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                schema: "dbo",
                table: "RoomServices",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Keep legacy global rows for InvoiceItems.ReferenceId history. Each property receives
            // its own catalog copy, and existing room assignments are remapped to that copy.
            migrationBuilder.Sql(@"
UPDATE rs SET CustomPrice = COALESCE(rs.CustomPrice, s.DefaultPrice)
FROM dbo.RoomServices AS rs JOIN dbo.Services AS s ON s.Id = rs.ServiceId;

INSERT INTO dbo.Services (Id, PropertyId, Name, Description, Unit, DefaultPrice, IsActive, CreatedAt)
SELECT NEWID(), p.Id, s.Name, s.Description, s.Unit, s.DefaultPrice, s.IsActive, s.CreatedAt
FROM dbo.Properties AS p CROSS JOIN dbo.Services AS s
WHERE s.PropertyId IS NULL;

UPDATE rs SET ServiceId = scoped.Id
FROM dbo.RoomServices AS rs
JOIN dbo.Rooms AS r ON r.Id = rs.RoomId
JOIN dbo.Services AS legacy ON legacy.Id = rs.ServiceId AND legacy.PropertyId IS NULL
JOIN dbo.Services AS scoped ON scoped.PropertyId = r.PropertyId AND scoped.Name = legacy.Name;

UPDATE dbo.Services SET IsActive = 0 WHERE PropertyId IS NULL;
");

            migrationBuilder.CreateIndex(
                name: "UX_Services_Property_Name",
                schema: "dbo",
                table: "Services",
                columns: new[] { "PropertyId", "Name" },
                unique: true,
                filter: "PropertyId IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RoomServices_Quantity",
                schema: "dbo",
                table: "RoomServices",
                sql: "(Quantity > 0)");

            migrationBuilder.AddForeignKey(
                name: "FK_Services_Properties_PropertyId",
                schema: "dbo",
                table: "Services",
                column: "PropertyId",
                principalSchema: "dbo",
                principalTable: "Properties",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM dbo.RoomServices WHERE Quantity <> 1)
    THROW 51000, 'Rollback requires handling room service quantities first.', 1;
IF EXISTS (
    SELECT 1 FROM dbo.Services AS scoped WHERE scoped.PropertyId IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM dbo.Services AS legacy WHERE legacy.PropertyId IS NULL AND legacy.Name = scoped.Name)
)
    THROW 51001, 'Rollback requires handling property-specific services first.', 1;
UPDATE rs SET ServiceId = legacy.Id
FROM dbo.RoomServices AS rs
JOIN dbo.Services AS scoped ON scoped.Id = rs.ServiceId AND scoped.PropertyId IS NOT NULL
JOIN dbo.Services AS legacy ON legacy.PropertyId IS NULL AND legacy.Name = scoped.Name;
DELETE FROM dbo.Services WHERE PropertyId IS NOT NULL;
");
            migrationBuilder.DropForeignKey(
                name: "FK_Services_Properties_PropertyId",
                schema: "dbo",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "UX_Services_Property_Name",
                schema: "dbo",
                table: "Services");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RoomServices_Quantity",
                schema: "dbo",
                table: "RoomServices");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                schema: "dbo",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "dbo",
                table: "RoomServices");

            migrationBuilder.CreateIndex(
                name: "UX_Services_Name",
                schema: "dbo",
                table: "Services",
                column: "Name",
                unique: true);
        }
    }
}
