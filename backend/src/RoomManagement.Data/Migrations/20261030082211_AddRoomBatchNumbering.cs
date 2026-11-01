using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomBatchNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RowId",
                schema: "dbo",
                table: "Rooms",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                schema: "dbo",
                table: "Rooms",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoomBatchSubmissions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomBatchSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomBatchSubmissions_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalSchema: "dbo",
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomNumberCounters",
                schema: "dbo",
                columns: table => new
                {
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastSequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomNumberCounters", x => new { x.PropertyId, x.ScopeKey });
                    table.ForeignKey(
                        name: "FK_RoomNumberCounters_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalSchema: "dbo",
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomRows",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomRows_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalSchema: "dbo",
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_PropertyId_RowId_Floor_SequenceNumber",
                schema: "dbo",
                table: "Rooms",
                columns: new[] { "PropertyId", "RowId", "Floor", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_RowId",
                schema: "dbo",
                table: "Rooms",
                column: "RowId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomBatchSubmissions_PropertyId_OwnerUserId_IdempotencyKey",
                schema: "dbo",
                table: "RoomBatchSubmissions",
                columns: new[] { "PropertyId", "OwnerUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomRows_PropertyId_Code",
                schema: "dbo",
                table: "RoomRows",
                columns: new[] { "PropertyId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_RoomRows_RowId",
                schema: "dbo",
                table: "Rooms",
                column: "RowId",
                principalSchema: "dbo",
                principalTable: "RoomRows",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_RoomRows_RowId",
                schema: "dbo",
                table: "Rooms");

            migrationBuilder.DropTable(
                name: "RoomBatchSubmissions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RoomNumberCounters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RoomRows",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_PropertyId_RowId_Floor_SequenceNumber",
                schema: "dbo",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_RowId",
                schema: "dbo",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "RowId",
                schema: "dbo",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                schema: "dbo",
                table: "Rooms");
        }
    }
}
