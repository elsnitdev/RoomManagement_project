using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementRefundEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundEvidenceBlobName",
                schema: "dbo",
                table: "ContractSettlements",
                type: "nvarchar(500)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundEvidenceContentType",
                schema: "dbo",
                table: "ContractSettlements",
                type: "nvarchar(30)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundEvidenceFileName",
                schema: "dbo",
                table: "ContractSettlements",
                type: "nvarchar(255)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RefundEvidenceId",
                schema: "dbo",
                table: "ContractSettlements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundEvidenceSha256",
                schema: "dbo",
                table: "ContractSettlements",
                type: "char(64)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RefundEvidenceSizeBytes",
                schema: "dbo",
                table: "ContractSettlements",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundEvidenceUploadedAt",
                schema: "dbo",
                table: "ContractSettlements",
                type: "datetime2(0)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundEvidenceBlobName",
                schema: "dbo",
                table: "ContractSettlements");

            migrationBuilder.DropColumn(
                name: "RefundEvidenceContentType",
                schema: "dbo",
                table: "ContractSettlements");

            migrationBuilder.DropColumn(
                name: "RefundEvidenceFileName",
                schema: "dbo",
                table: "ContractSettlements");

            migrationBuilder.DropColumn(
                name: "RefundEvidenceId",
                schema: "dbo",
                table: "ContractSettlements");

            migrationBuilder.DropColumn(
                name: "RefundEvidenceSha256",
                schema: "dbo",
                table: "ContractSettlements");

            migrationBuilder.DropColumn(
                name: "RefundEvidenceSizeBytes",
                schema: "dbo",
                table: "ContractSettlements");

            migrationBuilder.DropColumn(
                name: "RefundEvidenceUploadedAt",
                schema: "dbo",
                table: "ContractSettlements");
        }
    }
}
