using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSelfRegistrationAndContractConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.AddColumn<string>(
                name: "IdentityVerificationStatus",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(20)",
                nullable: false,
                defaultValueSql: "N'Unverified'")
                .Annotation("Relational:DefaultConstraintName", "DF_Users_IdentityVerificationStatus");

            migrationBuilder.AddColumn<DateTime>(
                name: "IdentityVerifiedAt",
                schema: "dbo",
                table: "Users",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ActiveRoom",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "Contracts",
                type: "nvarchar(40)",
                nullable: false,
                defaultValueSql: "N'Draft'",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldDefaultValueSql: "N'Draft'")
                .Annotation("Relational:DefaultConstraintName", "DF_Contracts_Status")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_Contracts_Status");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ActiveRoom",
                schema: "dbo",
                table: "Contracts",
                column: "RoomId",
                filter: "Status = N'Active'");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                schema: "dbo",
                table: "Contracts",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedByUserId",
                schema: "dbo",
                table: "Contracts",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OfferRulesVersionId",
                schema: "dbo",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfferSentAt",
                schema: "dbo",
                table: "Contracts",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfferSentByUserId",
                schema: "dbo",
                table: "Contracts",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfferSnapshotJson",
                schema: "dbo",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfferTermsHash",
                schema: "dbo",
                table: "Contracts",
                type: "varchar(128)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfferVersion",
                schema: "dbo",
                table: "Contracts",
                type: "int",
                nullable: false,
                defaultValueSql: "0")
                .Annotation("Relational:DefaultConstraintName", "DF_Contracts_OfferVersion");

            migrationBuilder.AddColumn<DateTime>(
                name: "OperatorConfirmedAt",
                schema: "dbo",
                table: "Contracts",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatorConfirmedByUserId",
                schema: "dbo",
                table: "Contracts",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TenantConfirmedAt",
                schema: "dbo",
                table: "Contracts",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantConfirmedByUserId",
                schema: "dbo",
                table: "Contracts",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantConfirmedOfferVersion",
                schema: "dbo",
                table: "Contracts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantConfirmedRulesVersionId",
                schema: "dbo",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantConfirmedTermsHash",
                schema: "dbo",
                table: "Contracts",
                type: "varchar(128)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TenantRejectedAt",
                schema: "dbo",
                table: "Contracts",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantRejectedByUserId",
                schema: "dbo",
                table: "Contracts",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantRejectionReason",
                schema: "dbo",
                table: "Contracts",
                type: "nvarchar(1000)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractOfferEvents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferVersion = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", nullable: false),
                    TermsHash = table.Column<string>(type: "varchar(128)", nullable: false),
                    RulesVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ActorIdentityVerificationStatus = table.Column<string>(type: "nvarchar(20)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ContractOfferEvents_CreatedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractOfferEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractOfferEvents_Contracts",
                        column: x => x.ContractId,
                        principalSchema: "dbo",
                        principalTable: "Contracts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenantLookupAudits",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OwnerLandlordUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsMatched = table.Column<bool>(type: "bit", nullable: false),
                    MaskedPhone = table.Column<string>(type: "varchar(30)", nullable: false),
                    MaskedCitizenId = table.Column<string>(type: "varchar(30)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_TenantLookupAudits_CreatedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantLookupAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(60)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_UserNotifications_IsRead"),
                    ReadAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_UserNotifications_CreatedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotifications_Users",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNumber",
                schema: "dbo",
                table: "Users",
                column: "PhoneNumber");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_IdentityVerificationStatus",
                schema: "dbo",
                table: "Users",
                sql: "(IdentityVerificationStatus IN (N'Unverified', N'Pending', N'Verified'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts",
                sql: "(Status IN (N'Draft', N'PendingTenantConfirmation', N'PendingOperatorConfirmation', N'Active', N'Expired', N'Terminated', N'Cancelled', N'PendingMoveOut', N'PendingSettlement'))");

            migrationBuilder.CreateIndex(
                name: "IX_ContractOfferEvents_Contract",
                schema: "dbo",
                table: "ContractOfferEvents",
                columns: new[] { "ContractId", "OfferVersion", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantLookupAudits_Actor_CreatedAt",
                schema: "dbo",
                table: "TenantLookupAudits",
                columns: new[] { "ActorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_User_Read_CreatedAt",
                schema: "dbo",
                table: "UserNotifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractOfferEvents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TenantLookupAudits",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UserNotifications",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Users_PhoneNumber",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_IdentityVerificationStatus",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IdentityVerificationStatus",
                schema: "dbo",
                table: "Users")
                .Annotation("Relational:DefaultConstraintName", "DF_Users_IdentityVerificationStatus");

            migrationBuilder.DropColumn(
                name: "IdentityVerifiedAt",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "OfferRulesVersionId",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "OfferSentAt",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "OfferSentByUserId",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "OfferSnapshotJson",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "OfferTermsHash",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "OfferVersion",
                schema: "dbo",
                table: "Contracts")
                .Annotation("Relational:DefaultConstraintName", "DF_Contracts_OfferVersion");

            migrationBuilder.DropColumn(
                name: "OperatorConfirmedAt",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "OperatorConfirmedByUserId",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TenantConfirmedAt",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TenantConfirmedByUserId",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TenantConfirmedOfferVersion",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TenantConfirmedRulesVersionId",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TenantConfirmedTermsHash",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TenantRejectedAt",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TenantRejectedByUserId",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TenantRejectionReason",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ActiveRoom",
                schema: "dbo",
                table: "Contracts");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "Contracts",
                type: "nvarchar(20)",
                nullable: false,
                defaultValueSql: "N'Draft'",
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldDefaultValueSql: "N'Draft'")
                .Annotation("Relational:DefaultConstraintName", "DF_Contracts_Status")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_Contracts_Status");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ActiveRoom",
                schema: "dbo",
                table: "Contracts",
                column: "RoomId",
                filter: "Status = N'Active'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contracts_Status",
                schema: "dbo",
                table: "Contracts",
                sql: "(Status IN (N'Draft', N'Active', N'Expired', N'Terminated', N'Cancelled', N'PendingMoveOut', N'PendingSettlement'))");
        }
    }
}
