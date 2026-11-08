using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261108120000_AddPaymentSubmitter")]
public sealed class AddPaymentSubmitter : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SubmittedByUserId", schema: "dbo", table: "Payments",
            type: "nvarchar(450)", nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_Payments_Submitter_Invoice", schema: "dbo", table: "Payments",
            columns: new[] { "SubmittedByUserId", "InvoiceId" });
        migrationBuilder.AddForeignKey(
            name: "FK_Payments_SubmittedBy", schema: "dbo", table: "Payments",
            column: "SubmittedByUserId", principalSchema: "dbo", principalTable: "Users",
            principalColumn: "Id", onDelete: ReferentialAction.NoAction);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Payments_SubmittedBy", schema: "dbo", table: "Payments");
        migrationBuilder.DropIndex(name: "IX_Payments_Submitter_Invoice", schema: "dbo", table: "Payments");
        migrationBuilder.DropColumn(name: "SubmittedByUserId", schema: "dbo", table: "Payments");
    }
}
