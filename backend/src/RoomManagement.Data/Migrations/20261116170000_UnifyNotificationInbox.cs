using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations;

[DbContext(typeof(RoomManagementDbContext))]
[Migration("20261116170000_UnifyNotificationInbox")]
public partial class UnifyNotificationInbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(name: "Title", schema: "dbo", table: "UserNotifications",
            type: "nvarchar(250)", nullable: false, oldClrType: typeof(string), oldType: "nvarchar(200)");
        migrationBuilder.AlterColumn<string>(name: "Message", schema: "dbo", table: "UserNotifications",
            type: "nvarchar(4000)", nullable: false, oldClrType: typeof(string), oldType: "nvarchar(1000)");
        migrationBuilder.AddColumn<Guid>(name: "InvoiceId", schema: "dbo", table: "UserNotifications",
            type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "RequestId", schema: "dbo", table: "UserNotifications",
            type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "EventKey", schema: "dbo", table: "UserNotifications",
            type: "nvarchar(200)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "EmailStatus", schema: "dbo", table: "UserNotifications",
            type: "nvarchar(25)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "EmailAddress", schema: "dbo", table: "UserNotifications",
            type: "nvarchar(256)", nullable: true);
        migrationBuilder.AddColumn<int>(name: "EmailAttempts", schema: "dbo", table: "UserNotifications",
            type: "int", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTime>(name: "EmailNextAttemptAt", schema: "dbo", table: "UserNotifications",
            type: "datetime2(0)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "EmailSentAt", schema: "dbo", table: "UserNotifications",
            type: "datetime2(0)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "EmailError", schema: "dbo", table: "UserNotifications",
            type: "nvarchar(500)", nullable: true);
        migrationBuilder.Sql("UPDATE dbo.UserNotifications SET EmailStatus = N'Legacy' WHERE EmailStatus IS NULL");
        migrationBuilder.CreateIndex(name: "UX_UserNotifications_User_EventKey", schema: "dbo",
            table: "UserNotifications", columns: new[] { "UserId", "EventKey" }, unique: true,
            filter: "[EventKey] IS NOT NULL");

        migrationBuilder.Sql("""
            INSERT INTO dbo.UserNotifications
                (Id, UserId, Type, Title, Message, PropertyId, ActorUserId, RequestId,
                 EventKey, IsRead, ReadAt, CreatedAt)
            SELECT NEWID(), rr.ReceiverUserId, N'Announcement', r.Title, r.Content,
                   r.PropertyId, r.SenderUserId, r.Id,
                   CONCAT(N'announcement:', CONVERT(nvarchar(36), r.Id)), rr.IsRead, rr.ReadAt, r.CreatedAt
            FROM dbo.Requests r
            INNER JOIN dbo.RequestRecipients rr ON rr.RequestId = r.Id
            WHERE r.Type = N'Announcement'
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.UserNotifications n
                  WHERE n.UserId = rr.ReceiverUserId
                    AND n.EventKey = CONCAT(N'announcement:', CONVERT(nvarchar(36), r.Id)));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "UX_UserNotifications_User_EventKey", schema: "dbo", table: "UserNotifications");
        migrationBuilder.DropColumn(name: "EventKey", schema: "dbo", table: "UserNotifications");
        migrationBuilder.DropColumn(name: "InvoiceId", schema: "dbo", table: "UserNotifications");
        migrationBuilder.DropColumn(name: "RequestId", schema: "dbo", table: "UserNotifications");
        migrationBuilder.DropColumn(name: "EmailStatus", schema: "dbo", table: "UserNotifications");
        migrationBuilder.DropColumn(name: "EmailAddress", schema: "dbo", table: "UserNotifications");
        migrationBuilder.DropColumn(name: "EmailAttempts", schema: "dbo", table: "UserNotifications");
        migrationBuilder.DropColumn(name: "EmailNextAttemptAt", schema: "dbo", table: "UserNotifications");
        migrationBuilder.DropColumn(name: "EmailSentAt", schema: "dbo", table: "UserNotifications");
        migrationBuilder.DropColumn(name: "EmailError", schema: "dbo", table: "UserNotifications");
        migrationBuilder.AlterColumn<string>(name: "Title", schema: "dbo", table: "UserNotifications",
            type: "nvarchar(200)", nullable: false, oldClrType: typeof(string), oldType: "nvarchar(250)");
        migrationBuilder.AlterColumn<string>(name: "Message", schema: "dbo", table: "UserNotifications",
            type: "nvarchar(1000)", nullable: false, oldClrType: typeof(string), oldType: "nvarchar(4000)");
    }
}
