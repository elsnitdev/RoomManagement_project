using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class MemberCommitmentsAndMoveOutNotice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantRequests_Category",
                schema: "dbo",
                table: "TenantRequests");

            migrationBuilder.CreateTable(
                name: "MemberCommitments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitmentType = table.Column<string>(type: "nvarchar(30)", nullable: false),
                    SignedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    SignerName = table.Column<string>(type: "nvarchar(200)", nullable: true),
                    SignerTenantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SignerRelation = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    TermsSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    ConfirmedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    EvidenceStatus = table.Column<string>(type: "nvarchar(32)", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberCommitments", x => x.Id);
                    table.CheckConstraint("CK_MemberCommitments_Evidence", "EvidenceStatus IN (N'PendingUpload', N'Uploaded', N'LegacyPendingEvidence')");
                    table.CheckConstraint("CK_MemberCommitments_Confirmation", "(EvidenceStatus = N'LegacyPendingEvidence' AND ConfirmedAt IS NULL AND ConfirmedByUserId IS NULL AND SignedOn IS NULL) OR (EvidenceStatus <> N'LegacyPendingEvidence' AND ConfirmedAt IS NOT NULL AND ConfirmedByUserId IS NOT NULL AND SignedOn IS NOT NULL AND SignerName IS NOT NULL AND TermsSnapshot IS NOT NULL)");
                    table.CheckConstraint("CK_MemberCommitments_Superseded", "SupersededAt IS NULL OR ConfirmedAt IS NULL OR SupersededAt >= ConfirmedAt");
                    table.CheckConstraint("CK_MemberCommitments_Type", "CommitmentType IN (N'RoomRulesAgreement')");
                    table.ForeignKey(
                        name: "FK_MemberCommitments_ConfirmedBy",
                        column: x => x.ConfirmedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MemberCommitments_ContractTenants",
                        column: x => x.ContractTenantId,
                        principalSchema: "dbo",
                        principalTable: "ContractTenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MemberCommitments_SignerTenantProfiles",
                        column: x => x.SignerTenantProfileId,
                        principalSchema: "dbo",
                        principalTable: "TenantProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantRequests_Category",
                schema: "dbo",
                table: "TenantRequests",
                sql: "(Category IN (N'Repair', N'Billing', N'Question', N'Complaint', N'Other', N'MoveOutNotice'))");

            migrationBuilder.CreateIndex(
                name: "IX_MemberCommitments_ConfirmedByUserId",
                schema: "dbo",
                table: "MemberCommitments",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberCommitments_SignerTenantProfileId",
                schema: "dbo",
                table: "MemberCommitments",
                column: "SignerTenantProfileId");

            migrationBuilder.CreateIndex(
                name: "UX_MemberCommitments_Current",
                schema: "dbo",
                table: "MemberCommitments",
                columns: new[] { "ContractTenantId", "CommitmentType" },
                unique: true,
                filter: "SupersededAt IS NULL");

            // Giữ trạng thái phòng và lịch sử cũ, không giả định người thuê đã ký cam kết.
            migrationBuilder.Sql("""
                INSERT INTO dbo.MemberCommitments
                    (Id, ContractTenantId, CommitmentType, EvidenceStatus, CreatedAt)
                SELECT NEWID(), ct.Id, N'RoomRulesAgreement', N'LegacyPendingEvidence', SYSUTCDATETIME()
                FROM dbo.ContractTenants AS ct
                INNER JOIN dbo.Contracts AS c ON c.Id = ct.ContractId
                WHERE c.Status IN (N'Active', N'Expired', N'Terminated');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberCommitments",
                schema: "dbo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantRequests_Category",
                schema: "dbo",
                table: "TenantRequests");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantRequests_Category",
                schema: "dbo",
                table: "TenantRequests",
                sql: "(Category IN (N'Repair', N'Billing', N'Question', N'Complaint', N'Other'))");
        }
    }
}
