using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationContractData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.AddColumn<DateTime>(
                name: "InvitationAcceptedAt",
                schema: "dbo",
                table: "ContractTenants",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitationAcceptedByUserId",
                schema: "dbo",
                table: "ContractTenants",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvitationAcceptedOfferVersion",
                schema: "dbo",
                table: "ContractTenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Workflow",
                schema: "dbo",
                table: "Contracts",
                type: "nvarchar(24)",
                nullable: false,
                defaultValueSql: "N'LegacyRules'")
                .Annotation("Relational:DefaultConstraintName", "DF_Contracts_Workflow");

            migrationBuilder.AddColumn<int>(
                name: "OfferVersion",
                schema: "dbo",
                table: "ContractDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PageOrder",
                schema: "dbo",
                table: "ContractDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                schema: "dbo",
                table: "ContractDocuments",
                type: "char(64)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                schema: "dbo",
                table: "ContractDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UploadedByUserId",
                schema: "dbo",
                table: "ContractDocuments",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts",
                sql: "(Status IN (N'Draft', N'PendingTenantConfirmation', N'PendingOperatorConfirmation', N'PendingStart', N'Active', N'Expired', N'Terminated', N'Cancelled', N'PendingMoveOut', N'PendingSettlement'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contracts_Workflow",
                schema: "dbo",
                table: "Contracts",
                sql: "(Workflow IN (N'LegacyRules', N'Invitation'))");

            migrationBuilder.CreateIndex(
                name: "IX_ContractDocuments_OfferPages",
                schema: "dbo",
                table: "ContractDocuments",
                columns: new[] { "ContractId", "OfferVersion", "PageOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contracts_Workflow",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_ContractDocuments_OfferPages",
                schema: "dbo",
                table: "ContractDocuments");

            migrationBuilder.DropColumn(
                name: "InvitationAcceptedAt",
                schema: "dbo",
                table: "ContractTenants");

            migrationBuilder.DropColumn(
                name: "InvitationAcceptedByUserId",
                schema: "dbo",
                table: "ContractTenants");

            migrationBuilder.DropColumn(
                name: "InvitationAcceptedOfferVersion",
                schema: "dbo",
                table: "ContractTenants");

            migrationBuilder.DropColumn(
                name: "Workflow",
                schema: "dbo",
                table: "Contracts")
                .Annotation("Relational:DefaultConstraintName", "DF_Contracts_Workflow");

            migrationBuilder.DropColumn(
                name: "OfferVersion",
                schema: "dbo",
                table: "ContractDocuments");

            migrationBuilder.DropColumn(
                name: "PageOrder",
                schema: "dbo",
                table: "ContractDocuments");

            migrationBuilder.DropColumn(
                name: "Sha256",
                schema: "dbo",
                table: "ContractDocuments");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                schema: "dbo",
                table: "ContractDocuments");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                schema: "dbo",
                table: "ContractDocuments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts",
                sql: "(Status IN (N'Draft', N'PendingTenantConfirmation', N'PendingOperatorConfirmation', N'Active', N'Expired', N'Terminated', N'Cancelled', N'PendingMoveOut', N'PendingSettlement'))");
        }
    }
}
