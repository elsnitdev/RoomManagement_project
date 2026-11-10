using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractRulesEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractRulesEvidence",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractRulesEvidence", x => x.Id);
                    table.CheckConstraint("CK_ContractRulesEvidence_Size", "[SizeBytes] > 0");
                    table.ForeignKey(
                        name: "FK_ContractRulesEvidence_ContractRulesAcceptances_AcceptanceId",
                        column: x => x.AcceptanceId,
                        principalSchema: "dbo",
                        principalTable: "ContractRulesAcceptances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractRulesEvidence_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractRulesEvidence_AcceptanceId",
                schema: "dbo",
                table: "ContractRulesEvidence",
                column: "AcceptanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractRulesEvidence_BlobName",
                schema: "dbo",
                table: "ContractRulesEvidence",
                column: "BlobName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractRulesEvidence_UploadedByUserId",
                schema: "dbo",
                table: "ContractRulesEvidence",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractRulesEvidence",
                schema: "dbo");
        }
    }
}
