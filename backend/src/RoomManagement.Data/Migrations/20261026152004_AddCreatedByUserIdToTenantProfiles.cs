using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByUserIdToTenantProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                schema: "dbo",
                table: "TenantProfiles",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantProfiles_CreatedByUserId",
                schema: "dbo",
                table: "TenantProfiles",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantProfiles_CreatedByUser",
                schema: "dbo",
                table: "TenantProfiles",
                column: "CreatedByUserId",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TenantProfiles_CreatedByUser",
                schema: "dbo",
                table: "TenantProfiles");

            migrationBuilder.DropIndex(
                name: "IX_TenantProfiles_CreatedByUserId",
                schema: "dbo",
                table: "TenantProfiles");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "dbo",
                table: "TenantProfiles");
        }
    }
}
