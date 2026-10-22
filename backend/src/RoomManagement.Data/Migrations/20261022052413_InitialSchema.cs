using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "Services",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    DefaultPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_Services_IsActive"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Services_CreatedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.CheckConstraint("CK_Services_DefaultPrice", "(DefaultPrice IS NULL OR DefaultPrice >= 0)");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", nullable: false),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_Users_IsActive"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Users_CreatedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_Role", "(Role IN (N'Admin', N'Landlord', N'Tenant'))");
                });

            migrationBuilder.CreateTable(
                name: "Properties",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_Properties_IsActive"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Properties_CreatedAt"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Properties_Owner",
                        column: x => x.OwnerUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenantProfiles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "varchar(20)", nullable: false),
                    Email = table.Column<string>(type: "varchar(150)", nullable: true),
                    CitizenId = table.Column<string>(type: "varchar(20)", nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(10)", nullable: true),
                    PermanentAddress = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_TenantProfiles_CreatedAt"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantProfiles", x => x.Id);
                    table.CheckConstraint("CK_TenantProfiles_Gender", "(Gender IS NULL OR Gender IN (N'Male', N'Female', N'Other'))");
                    table.ForeignKey(
                        name: "FK_TenantProfiles_Users",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomNumber = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    RoomName = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    Floor = table.Column<int>(type: "int", nullable: true),
                    Area = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxOccupants = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false, defaultValueSql: "N'Available'")
                        .Annotation("Relational:DefaultConstraintName", "DF_Rooms_Status"),
                    Description = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Rooms_CreatedAt"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.CheckConstraint("CK_Rooms_Amounts", "(BasePrice >= 0 AND MaxOccupants > 0 AND (Area IS NULL OR Area > 0))");
                    table.CheckConstraint("CK_Rooms_Status", "(Status IN (N'Available', N'Occupied', N'Maintenance', N'Inactive'))");
                    table.ForeignKey(
                        name: "FK_Rooms_Properties",
                        column: x => x.PropertyId,
                        principalSchema: "dbo",
                        principalTable: "Properties",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UtilityRates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UtilityType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_UtilityRates_CreatedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityRates", x => x.Id);
                    table.CheckConstraint("CK_UtilityRates_Dates", "(EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)");
                    table.CheckConstraint("CK_UtilityRates_Price", "(UnitPrice >= 0)");
                    table.CheckConstraint("CK_UtilityRates_Type", "(UtilityType IN (N'Electricity', N'Water'))");
                    table.ForeignKey(
                        name: "FK_UtilityRates_Properties",
                        column: x => x.PropertyId,
                        principalSchema: "dbo",
                        principalTable: "Properties",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractCode = table.Column<string>(type: "varchar(50)", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MonthlyRent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_Contracts_Deposit"),
                    PaymentDueDay = table.Column<byte>(type: "tinyint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false, defaultValueSql: "N'Draft'")
                        .Annotation("Relational:DefaultConstraintName", "DF_Contracts_Status"),
                    Note = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Contracts_CreatedAt"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.UniqueConstraint("UQ_Contracts_Id_Room", x => new { x.Id, x.RoomId });
                    table.CheckConstraint("CK_Contracts_Amounts", "(MonthlyRent >= 0 AND DepositAmount >= 0)");
                    table.CheckConstraint("CK_Contracts_Dates", "(EndDate IS NULL OR EndDate >= StartDate)");
                    table.CheckConstraint("CK_Contracts_DueDay", "(PaymentDueDay IS NULL OR PaymentDueDay BETWEEN 1 AND 31)");
                    table.CheckConstraint("CK_Contracts_Status", "(Status IN (N'Draft', N'Active', N'Expired', N'Terminated', N'Cancelled'))");
                    table.ForeignKey(
                        name: "FK_Contracts_Rooms",
                        column: x => x.RoomId,
                        principalSchema: "dbo",
                        principalTable: "Rooms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MeterReadings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeterType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    Month = table.Column<byte>(type: "tinyint", nullable: false),
                    PreviousValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_MeterReadings_RecordedAt"),
                    Note = table.Column<string>(type: "nvarchar(500)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterReadings", x => x.Id);
                    table.CheckConstraint("CK_MeterReadings_Period", "([Year] BETWEEN 2000 AND 2100 AND [Month] BETWEEN 1 AND 12)");
                    table.CheckConstraint("CK_MeterReadings_Type", "(MeterType IN (N'Electricity', N'Water'))");
                    table.CheckConstraint("CK_MeterReadings_Values", "(PreviousValue >= 0 AND CurrentValue >= PreviousValue)");
                    table.ForeignKey(
                        name: "FK_MeterReadings_Rooms",
                        column: x => x.RoomId,
                        principalSchema: "dbo",
                        principalTable: "Rooms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RoomServices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_RoomServices_CreatedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomServices", x => x.Id);
                    table.CheckConstraint("CK_RoomServices_Dates", "(EndDate IS NULL OR EndDate >= StartDate)");
                    table.CheckConstraint("CK_RoomServices_Price", "(CustomPrice IS NULL OR CustomPrice >= 0)");
                    table.ForeignKey(
                        name: "FK_RoomServices_Rooms",
                        column: x => x.RoomId,
                        principalSchema: "dbo",
                        principalTable: "Rooms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RoomServices_Services",
                        column: x => x.ServiceId,
                        principalSchema: "dbo",
                        principalTable: "Services",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContractDocuments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ContractDocuments_UploadedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractDocuments", x => x.Id);
                    table.CheckConstraint("CK_ContractDocuments_Type", "(DocumentType IN (N'ContractPage', N'CitizenId', N'Handover', N'Appendix', N'Other'))");
                    table.ForeignKey(
                        name: "FK_ContractDocuments_Contracts",
                        column: x => x.ContractId,
                        principalSchema: "dbo",
                        principalTable: "Contracts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContractSettlements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_ContractSettlements_Outstanding"),
                    DamageFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_ContractSettlements_Damage"),
                    OtherFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_ContractSettlements_Other"),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_ContractSettlements_Refund"),
                    Note = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ContractSettlements_CreatedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractSettlements", x => x.Id);
                    table.CheckConstraint("CK_ContractSettlements_Amounts", "(DepositAmount >= 0 AND OutstandingAmount >= 0 AND DamageFee >= 0 AND OtherFee >= 0 AND RefundAmount >= 0)");
                    table.ForeignKey(
                        name: "FK_ContractSettlements_Contracts",
                        column: x => x.ContractId,
                        principalSchema: "dbo",
                        principalTable: "Contracts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContractTenants",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LeaveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ContractTenants_CreatedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTenants", x => x.Id);
                    table.CheckConstraint("CK_ContractTenants_Dates", "(LeaveDate IS NULL OR LeaveDate >= JoinDate)");
                    table.ForeignKey(
                        name: "FK_ContractTenants_Contracts",
                        column: x => x.ContractId,
                        principalSchema: "dbo",
                        principalTable: "Contracts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractTenants_Tenants",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "TenantProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceCode = table.Column<string>(type: "varchar(50)", nullable: false),
                    InvoiceType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    BillingYear = table.Column<short>(type: "smallint", nullable: true),
                    BillingMonth = table.Column<byte>(type: "tinyint", nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false, defaultValueSql: "N'Draft'")
                        .Annotation("Relational:DefaultConstraintName", "DF_Invoices_Status"),
                    Note = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Invoices_CreatedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.UniqueConstraint("UQ_Invoices_Id_Contract", x => new { x.Id, x.ContractId });
                    table.CheckConstraint("CK_Invoices_Dates", "(DueDate >= IssueDate)");
                    table.CheckConstraint("CK_Invoices_MonthlyPeriod", "(InvoiceType <> N'Monthly' OR BillingYear IS NOT NULL)");
                    table.CheckConstraint("CK_Invoices_Period", "((BillingYear IS NULL AND BillingMonth IS NULL) OR (BillingYear IS NOT NULL AND BillingMonth IS NOT NULL AND BillingYear BETWEEN 2000 AND 2100 AND BillingMonth BETWEEN 1 AND 12))");
                    table.CheckConstraint("CK_Invoices_Status", "(Status IN (N'Draft', N'Issued', N'Cancelled'))");
                    table.CheckConstraint("CK_Invoices_Total", "(TotalAmount >= 0)");
                    table.CheckConstraint("CK_Invoices_Type", "(InvoiceType IN (N'Monthly', N'Deposit', N'Damage', N'Settlement', N'Other'))");
                    table.ForeignKey(
                        name: "FK_Invoices_ContractRoom",
                        columns: x => new { x.ContractId, x.RoomId },
                        principalSchema: "dbo",
                        principalTable: "Contracts",
                        principalColumns: new[] { "Id", "RoomId" });
                });

            migrationBuilder.CreateTable(
                name: "DepositApplications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(100)", nullable: false),
                    AppliedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_DepositApplications_AppliedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepositApplications", x => x.Id);
                    table.CheckConstraint("CK_DepositApplications_Amount", "(Amount > 0)");
                    table.ForeignKey(
                        name: "FK_DepositApplications_AppliedBy",
                        column: x => x.AppliedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DepositApplications_Contracts",
                        column: x => x.ContractId,
                        principalSchema: "dbo",
                        principalTable: "Contracts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DepositApplications_TargetInvoice",
                        columns: x => new { x.TargetInvoiceId, x.ContractId },
                        principalSchema: "dbo",
                        principalTable: "Invoices",
                        principalColumns: new[] { "Id", "ContractId" });
                });

            migrationBuilder.CreateTable(
                name: "InvoiceAdjustments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjustmentType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(100)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_InvoiceAdjustments_CreatedAt"),
                    VoidedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    VoidedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceAdjustments", x => x.Id);
                    table.CheckConstraint("CK_InvoiceAdjustments_Amount", "(Amount > 0)");
                    table.CheckConstraint("CK_InvoiceAdjustments_Type", "(AdjustmentType IN (N'Discount', N'Correction', N'WriteOff'))");
                    table.CheckConstraint("CK_InvoiceAdjustments_Void", "((VoidedAt IS NULL AND VoidReason IS NULL AND VoidedByUserId IS NULL) OR (VoidedAt IS NOT NULL AND VoidReason IS NOT NULL AND VoidedByUserId IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_InvoiceAdjustments_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceAdjustments_Invoices",
                        column: x => x.InvoiceId,
                        principalSchema: "dbo",
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceAdjustments_VoidedBy",
                        column: x => x.VoidedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_InvoiceItems_Quantity"),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_InvoiceItems_CreatedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceItems", x => x.Id);
                    table.CheckConstraint("CK_InvoiceItems_Amounts", "(Quantity > 0 AND UnitPrice >= 0 AND Amount = CASE WHEN ItemType = N'Discount' THEN -CONVERT(decimal(18,2), ROUND(Quantity * UnitPrice, 2)) ELSE CONVERT(decimal(18,2), ROUND(Quantity * UnitPrice, 2)) END)");
                    table.CheckConstraint("CK_InvoiceItems_Type", "(ItemType IN (N'Rent', N'Deposit', N'Electricity', N'Water', N'Service', N'Damage', N'Penalty', N'Discount', N'Other'))");
                    table.ForeignKey(
                        name: "FK_InvoiceItems_Invoices",
                        column: x => x.InvoiceId,
                        principalSchema: "dbo",
                        principalTable: "Invoices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VerifiedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    VerifiedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TransactionCode = table.Column<string>(type: "varchar(150)", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "varchar(100)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Payments_CreatedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_ClaimedAmount", "(ClaimedAmount > 0)");
                    table.CheckConstraint("CK_Payments_Method", "(PaymentMethod IN (N'Cash', N'BankTransfer', N'OnlinePayment', N'Other'))");
                    table.CheckConstraint("CK_Payments_Status", "(Status IN (N'Pending', N'Success', N'Failed', N'Cancelled'))");
                    table.CheckConstraint("CK_Payments_Verification", "((Status = N'Success' AND VerifiedAmount IS NOT NULL AND VerifiedAmount > 0 AND PaidAt IS NOT NULL AND VerifiedAt IS NOT NULL AND VerifiedByUserId IS NOT NULL) OR (Status <> N'Success' AND VerifiedAmount IS NULL AND VerifiedAt IS NULL AND VerifiedByUserId IS NULL))");
                    table.ForeignKey(
                        name: "FK_Payments_Invoices",
                        column: x => x.InvoiceId,
                        principalSchema: "dbo",
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payments_VerifiedBy",
                        column: x => x.VerifiedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    RefundedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", nullable: false),
                    TransactionCode = table.Column<string>(type: "varchar(150)", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "varchar(100)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Refunds_CreatedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.CheckConstraint("CK_Refunds_Amount", "(Amount > 0)");
                    table.CheckConstraint("CK_Refunds_RefundedAt", "(Status <> N'Success' OR RefundedAt IS NOT NULL)");
                    table.CheckConstraint("CK_Refunds_Status", "(Status IN (N'Pending', N'Success', N'Failed', N'Cancelled'))");
                    table.ForeignKey(
                        name: "FK_Refunds_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Refunds_Invoices",
                        column: x => x.InvoiceId,
                        principalSchema: "dbo",
                        principalTable: "Invoices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenantRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelatedInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(10)", nullable: false, defaultValueSql: "N'Medium'")
                        .Annotation("Relational:DefaultConstraintName", "DF_TenantRequests_Priority"),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false, defaultValueSql: "N'Pending'")
                        .Annotation("Relational:DefaultConstraintName", "DF_TenantRequests_Status"),
                    AssignedToUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_TenantRequests_RequestedAt"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantRequests", x => x.Id);
                    table.CheckConstraint("CK_TenantRequests_Category", "(Category IN (N'Repair', N'Billing', N'Question', N'Complaint', N'Other'))");
                    table.CheckConstraint("CK_TenantRequests_Priority", "(Priority IN (N'Low', N'Medium', N'High'))");
                    table.CheckConstraint("CK_TenantRequests_ResolvedAt", "((Status = N'Resolved' AND ResolvedAt IS NOT NULL) OR (Status <> N'Resolved' AND ResolvedAt IS NULL))");
                    table.CheckConstraint("CK_TenantRequests_Status", "(Status IN (N'Pending', N'InProgress', N'Resolved'))");
                    table.ForeignKey(
                        name: "FK_TenantRequests_AssignedTo",
                        column: x => x.AssignedToUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenantRequests_Contracts",
                        column: x => x.ContractId,
                        principalSchema: "dbo",
                        principalTable: "Contracts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenantRequests_Invoices",
                        columns: x => new { x.RelatedInvoiceId, x.ContractId },
                        principalSchema: "dbo",
                        principalTable: "Invoices",
                        principalColumns: new[] { "Id", "ContractId" });
                    table.ForeignKey(
                        name: "FK_TenantRequests_Tenants",
                        column: x => x.RequestedByTenantId,
                        principalSchema: "dbo",
                        principalTable: "TenantProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentReviewEvents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    PreviousVerifiedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NewVerifiedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PaymentReviewEvents_ReviewedAt")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReviewEvents", x => x.Id);
                    table.CheckConstraint("CK_PaymentReviewEvents_Amounts", "((PreviousVerifiedAmount IS NULL OR PreviousVerifiedAmount > 0) AND (NewVerifiedAmount IS NULL OR NewVerifiedAmount > 0))");
                    table.CheckConstraint("CK_PaymentReviewEvents_NewStatus", "(NewStatus IN (N'Pending', N'Success', N'Failed', N'Cancelled'))");
                    table.CheckConstraint("CK_PaymentReviewEvents_PreviousStatus", "(PreviousStatus IN (N'Pending', N'Success', N'Failed', N'Cancelled'))");
                    table.ForeignKey(
                        name: "FK_PaymentReviewEvents_Payments",
                        column: x => x.PaymentId,
                        principalSchema: "dbo",
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentReviewEvents_ReviewedBy",
                        column: x => x.ReviewedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractDocuments_ContractId",
                schema: "dbo",
                table: "ContractDocuments",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Room_Dates",
                schema: "dbo",
                table: "Contracts",
                columns: new[] { "RoomId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "UX_Contracts_Code",
                schema: "dbo",
                table: "Contracts",
                column: "ContractCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Contracts_OneActivePerRoom",
                schema: "dbo",
                table: "Contracts",
                column: "RoomId",
                unique: true,
                filter: "Status = N'Active'");

            migrationBuilder.CreateIndex(
                name: "UX_ContractSettlements_ContractId",
                schema: "dbo",
                table: "ContractSettlements",
                column: "ContractId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractTenants_TenantId",
                schema: "dbo",
                table: "ContractTenants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_ContractTenants_Join",
                schema: "dbo",
                table: "ContractTenants",
                columns: new[] { "ContractId", "TenantId", "JoinDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepositApplications_AppliedByUserId",
                schema: "dbo",
                table: "DepositApplications",
                column: "AppliedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositApplications_ContractId",
                schema: "dbo",
                table: "DepositApplications",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositApplications_TargetInvoiceId",
                schema: "dbo",
                table: "DepositApplications",
                column: "TargetInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositApplications_TargetInvoiceId_ContractId",
                schema: "dbo",
                table: "DepositApplications",
                columns: new[] { "TargetInvoiceId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "UX_DepositApplications_IdempotencyKey",
                schema: "dbo",
                table: "DepositApplications",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAdjustments_CreatedByUserId",
                schema: "dbo",
                table: "InvoiceAdjustments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAdjustments_InvoiceId",
                schema: "dbo",
                table: "InvoiceAdjustments",
                columns: new[] { "InvoiceId", "VoidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAdjustments_VoidedByUserId",
                schema: "dbo",
                table: "InvoiceAdjustments",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_InvoiceAdjustments_IdempotencyKey",
                schema: "dbo",
                table: "InvoiceAdjustments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_InvoiceId",
                schema: "dbo",
                table: "InvoiceItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Contract_Status",
                schema: "dbo",
                table: "Invoices",
                columns: new[] { "ContractId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId_RoomId",
                schema: "dbo",
                table: "Invoices",
                columns: new[] { "ContractId", "RoomId" });

            migrationBuilder.CreateIndex(
                name: "UX_Invoices_Code",
                schema: "dbo",
                table: "Invoices",
                column: "InvoiceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Invoices_Deposit",
                schema: "dbo",
                table: "Invoices",
                column: "ContractId",
                unique: true,
                filter: "InvoiceType = N'Deposit' AND Status <> N'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "UX_Invoices_Monthly",
                schema: "dbo",
                table: "Invoices",
                columns: new[] { "ContractId", "BillingYear", "BillingMonth" },
                unique: true,
                filter: "InvoiceType = N'Monthly' AND Status <> N'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "UX_MeterReadings_Period",
                schema: "dbo",
                table: "MeterReadings",
                columns: new[] { "RoomId", "MeterType", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReviewEvents_Payment_ReviewedAt",
                schema: "dbo",
                table: "PaymentReviewEvents",
                columns: new[] { "PaymentId", "ReviewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReviewEvents_ReviewedByUserId",
                schema: "dbo",
                table: "PaymentReviewEvents",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Invoice_Status",
                schema: "dbo",
                table: "Payments",
                columns: new[] { "InvoiceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_VerifiedByUserId",
                schema: "dbo",
                table: "Payments",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Payments_IdempotencyKey",
                schema: "dbo",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Payments_TransactionCode",
                schema: "dbo",
                table: "Payments",
                column: "TransactionCode",
                unique: true,
                filter: "TransactionCode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_OwnerUserId",
                schema: "dbo",
                table: "Properties",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_CreatedByUserId",
                schema: "dbo",
                table: "Refunds",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_Invoice_Status",
                schema: "dbo",
                table: "Refunds",
                columns: new[] { "InvoiceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_Refunds_IdempotencyKey",
                schema: "dbo",
                table: "Refunds",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Refunds_TransactionCode",
                schema: "dbo",
                table: "Refunds",
                column: "TransactionCode",
                unique: true,
                filter: "TransactionCode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_Property_Status",
                schema: "dbo",
                table: "Rooms",
                columns: new[] { "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_Rooms_Property_Number",
                schema: "dbo",
                table: "Rooms",
                columns: new[] { "PropertyId", "RoomNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomServices_ServiceId",
                schema: "dbo",
                table: "RoomServices",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "UX_RoomServices_Open",
                schema: "dbo",
                table: "RoomServices",
                columns: new[] { "RoomId", "ServiceId" },
                unique: true,
                filter: "EndDate IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_RoomServices_Start",
                schema: "dbo",
                table: "RoomServices",
                columns: new[] { "RoomId", "ServiceId", "StartDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Services_Name",
                schema: "dbo",
                table: "Services",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TenantProfiles_CitizenId",
                schema: "dbo",
                table: "TenantProfiles",
                column: "CitizenId",
                unique: true,
                filter: "CitizenId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_TenantProfiles_UserId",
                schema: "dbo",
                table: "TenantProfiles",
                column: "UserId",
                unique: true,
                filter: "UserId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRequests_Assigned_Status",
                schema: "dbo",
                table: "TenantRequests",
                columns: new[] { "AssignedToUserId", "Status" },
                filter: "AssignedToUserId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRequests_Contract_Status",
                schema: "dbo",
                table: "TenantRequests",
                columns: new[] { "ContractId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantRequests_RelatedInvoiceId_ContractId",
                schema: "dbo",
                table: "TenantRequests",
                columns: new[] { "RelatedInvoiceId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantRequests_Tenant_Status",
                schema: "dbo",
                table: "TenantRequests",
                columns: new[] { "RequestedByTenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_Users_NormalizedEmail",
                schema: "dbo",
                table: "Users",
                column: "NormalizedEmail",
                unique: true,
                filter: "NormalizedEmail IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Users_NormalizedUserName",
                schema: "dbo",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_UtilityRates_Open",
                schema: "dbo",
                table: "UtilityRates",
                columns: new[] { "PropertyId", "UtilityType" },
                unique: true,
                filter: "EffectiveTo IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_UtilityRates_Start",
                schema: "dbo",
                table: "UtilityRates",
                columns: new[] { "PropertyId", "UtilityType", "EffectiveFrom" },
                unique: true);

            // CREATE VIEW must be the only statement in its SQL Server batch.
            // EXEC also keeps idempotent migration scripts valid.
            migrationBuilder.Sql("EXEC(N'" + vwInvoiceBalancesSql.Replace("'", "''") + "')");
            migrationBuilder.Sql("EXEC(N'" + vwContractDepositBalancesSql.Replace("'", "''") + "')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("EXEC(N'DROP VIEW dbo.vwContractDepositBalances')");
            migrationBuilder.Sql("EXEC(N'DROP VIEW dbo.vwInvoiceBalances')");

            migrationBuilder.DropTable(
                name: "ContractDocuments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ContractSettlements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ContractTenants",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DepositApplications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "InvoiceAdjustments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "InvoiceItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MeterReadings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PaymentReviewEvents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Refunds",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RoomServices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TenantRequests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UtilityRates",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Services",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TenantProfiles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Contracts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Rooms",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Properties",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "dbo");
        }
    }
}
