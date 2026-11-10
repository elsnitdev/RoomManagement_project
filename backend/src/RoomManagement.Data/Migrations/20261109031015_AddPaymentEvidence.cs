using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentEvidence",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEvidence", x => x.Id);
                    table.CheckConstraint("CK_PaymentEvidence_Size", "[SizeBytes] > 0");
                    table.ForeignKey(
                        name: "FK_PaymentEvidence_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "dbo",
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentEvidence_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvidence_BlobName",
                schema: "dbo",
                table: "PaymentEvidence",
                column: "BlobName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvidence_PaymentId_CreatedAt",
                schema: "dbo",
                table: "PaymentEvidence",
                columns: new[] { "PaymentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvidence_UploadedByUserId",
                schema: "dbo",
                table: "PaymentEvidence",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentEvidence",
                schema: "dbo");
        }
    }
}
