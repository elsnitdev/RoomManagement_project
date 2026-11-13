using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261113123000_AddTwoWayRequestsAndAnnouncements")]
public partial class AddTwoWayRequestsAndAnnouncements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Requests",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SenderUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                RecipientType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                Type = table.Column<string>(type: "nvarchar(20)", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Requests", x => x.Id);
                table.CheckConstraint("CK_Requests_RecipientType", "(RecipientType IN (N'Single', N'Multiple', N'All', N'Owner'))");
                table.CheckConstraint("CK_Requests_Type", "(Type IN (N'Request', N'Announcement'))");
                table.ForeignKey(
                    name: "FK_Requests_Properties",
                    column: x => x.PropertyId,
                    principalSchema: "dbo",
                    principalTable: "Properties",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Requests_Sender",
                    column: x => x.SenderUserId,
                    principalSchema: "dbo",
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "RequestRecipients",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReceiverUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "0"),
                ReadAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                Status = table.Column<string>(type: "nvarchar(20)", nullable: false, defaultValueSql: "N'Open'")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RequestRecipients", x => x.Id);
                table.CheckConstraint("CK_RequestRecipients_Status", "(Status IN (N'Open', N'InProgress', N'Resolved', N'Closed'))");
                table.ForeignKey(
                    name: "FK_RequestRecipients_Receiver",
                    column: x => x.ReceiverUserId,
                    principalSchema: "dbo",
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RequestRecipients_Requests",
                    column: x => x.RequestId,
                    principalSchema: "dbo",
                    principalTable: "Requests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Requests_PropertyId_Type_CreatedAt",
            schema: "dbo",
            table: "Requests",
            columns: new[] { "PropertyId", "Type", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Requests_SenderUserId_Type_CreatedAt",
            schema: "dbo",
            table: "Requests",
            columns: new[] { "SenderUserId", "Type", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_RequestRecipients_ReceiverUserId_Status_IsRead",
            schema: "dbo",
            table: "RequestRecipients",
            columns: new[] { "ReceiverUserId", "Status", "IsRead" });

        migrationBuilder.CreateIndex(
            name: "IX_RequestRecipients_RequestId_ReceiverUserId",
            schema: "dbo",
            table: "RequestRecipients",
            columns: new[] { "RequestId", "ReceiverUserId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RequestRecipients",
            schema: "dbo");

        migrationBuilder.DropTable(
            name: "Requests",
            schema: "dbo");
    }
}

