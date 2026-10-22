IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Services] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Unit] nvarchar(50) NULL,
        [DefaultPrice] decimal(18,2) NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_Services_IsActive] DEFAULT (1),
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_Services_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Services] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Services_DefaultPrice] CHECK ((DefaultPrice IS NULL OR DefaultPrice >= 0))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id] nvarchar(450) NOT NULL,
        [UserName] nvarchar(256) NOT NULL,
        [NormalizedUserName] nvarchar(256) NOT NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_Users_IsActive] DEFAULT (1),
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_Users_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Users_Role] CHECK ((Role IN (N'Admin', N'Landlord', N'Tenant')))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Properties] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerUserId] nvarchar(450) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Address] nvarchar(300) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_Properties_IsActive] DEFAULT (1),
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_Properties_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2(0) NULL,
        CONSTRAINT [PK_Properties] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Properties_Owner] FOREIGN KEY ([OwnerUserId]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[TenantProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NULL,
        [FullName] nvarchar(150) NOT NULL,
        [PhoneNumber] varchar(20) NOT NULL,
        [Email] varchar(150) NULL,
        [CitizenId] varchar(20) NULL,
        [DateOfBirth] date NULL,
        [Gender] nvarchar(10) NULL,
        [PermanentAddress] nvarchar(300) NULL,
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_TenantProfiles_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2(0) NULL,
        CONSTRAINT [PK_TenantProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TenantProfiles_Gender] CHECK ((Gender IS NULL OR Gender IN (N'Male', N'Female', N'Other'))),
        CONSTRAINT [FK_TenantProfiles_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Rooms] (
        [Id] uniqueidentifier NOT NULL,
        [PropertyId] uniqueidentifier NOT NULL,
        [RoomNumber] nvarchar(50) NOT NULL,
        [RoomName] nvarchar(100) NULL,
        [Floor] int NULL,
        [Area] decimal(10,2) NULL,
        [BasePrice] decimal(18,2) NOT NULL,
        [MaxOccupants] int NOT NULL,
        [Status] nvarchar(20) NOT NULL CONSTRAINT [DF_Rooms_Status] DEFAULT (N'Available'),
        [Description] nvarchar(1000) NULL,
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_Rooms_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2(0) NULL,
        CONSTRAINT [PK_Rooms] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Rooms_Amounts] CHECK ((BasePrice >= 0 AND MaxOccupants > 0 AND (Area IS NULL OR Area > 0))),
        CONSTRAINT [CK_Rooms_Status] CHECK ((Status IN (N'Available', N'Occupied', N'Maintenance', N'Inactive'))),
        CONSTRAINT [FK_Rooms_Properties] FOREIGN KEY ([PropertyId]) REFERENCES [dbo].[Properties] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[UtilityRates] (
        [Id] uniqueidentifier NOT NULL,
        [PropertyId] uniqueidentifier NOT NULL,
        [UtilityType] nvarchar(20) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [Unit] nvarchar(50) NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_UtilityRates_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_UtilityRates] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_UtilityRates_Dates] CHECK ((EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)),
        CONSTRAINT [CK_UtilityRates_Price] CHECK ((UnitPrice >= 0)),
        CONSTRAINT [CK_UtilityRates_Type] CHECK ((UtilityType IN (N'Electricity', N'Water'))),
        CONSTRAINT [FK_UtilityRates_Properties] FOREIGN KEY ([PropertyId]) REFERENCES [dbo].[Properties] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Contracts] (
        [Id] uniqueidentifier NOT NULL,
        [ContractCode] varchar(50) NOT NULL,
        [RoomId] uniqueidentifier NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [MonthlyRent] decimal(18,2) NOT NULL,
        [DepositAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_Contracts_Deposit] DEFAULT (0),
        [PaymentDueDay] tinyint NULL,
        [Status] nvarchar(20) NOT NULL CONSTRAINT [DF_Contracts_Status] DEFAULT (N'Draft'),
        [Note] nvarchar(1000) NULL,
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_Contracts_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2(0) NULL,
        CONSTRAINT [PK_Contracts] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_Contracts_Id_Room] UNIQUE ([Id], [RoomId]),
        CONSTRAINT [CK_Contracts_Amounts] CHECK ((MonthlyRent >= 0 AND DepositAmount >= 0)),
        CONSTRAINT [CK_Contracts_Dates] CHECK ((EndDate IS NULL OR EndDate >= StartDate)),
        CONSTRAINT [CK_Contracts_DueDay] CHECK ((PaymentDueDay IS NULL OR PaymentDueDay BETWEEN 1 AND 31)),
        CONSTRAINT [CK_Contracts_Status] CHECK ((Status IN (N'Draft', N'Active', N'Expired', N'Terminated', N'Cancelled'))),
        CONSTRAINT [FK_Contracts_Rooms] FOREIGN KEY ([RoomId]) REFERENCES [dbo].[Rooms] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[MeterReadings] (
        [Id] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NOT NULL,
        [MeterType] nvarchar(20) NOT NULL,
        [Year] smallint NOT NULL,
        [Month] tinyint NOT NULL,
        [PreviousValue] decimal(18,2) NOT NULL,
        [CurrentValue] decimal(18,2) NOT NULL,
        [RecordedAt] datetime2(0) NOT NULL CONSTRAINT [DF_MeterReadings_RecordedAt] DEFAULT (SYSUTCDATETIME()),
        [Note] nvarchar(500) NULL,
        CONSTRAINT [PK_MeterReadings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MeterReadings_Period] CHECK (([Year] BETWEEN 2000 AND 2100 AND [Month] BETWEEN 1 AND 12)),
        CONSTRAINT [CK_MeterReadings_Type] CHECK ((MeterType IN (N'Electricity', N'Water'))),
        CONSTRAINT [CK_MeterReadings_Values] CHECK ((PreviousValue >= 0 AND CurrentValue >= PreviousValue)),
        CONSTRAINT [FK_MeterReadings_Rooms] FOREIGN KEY ([RoomId]) REFERENCES [dbo].[Rooms] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[RoomServices] (
        [Id] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NOT NULL,
        [ServiceId] uniqueidentifier NOT NULL,
        [CustomPrice] decimal(18,2) NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_RoomServices_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_RoomServices] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_RoomServices_Dates] CHECK ((EndDate IS NULL OR EndDate >= StartDate)),
        CONSTRAINT [CK_RoomServices_Price] CHECK ((CustomPrice IS NULL OR CustomPrice >= 0)),
        CONSTRAINT [FK_RoomServices_Rooms] FOREIGN KEY ([RoomId]) REFERENCES [dbo].[Rooms] ([Id]),
        CONSTRAINT [FK_RoomServices_Services] FOREIGN KEY ([ServiceId]) REFERENCES [dbo].[Services] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ContractDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [StorageKey] nvarchar(500) NOT NULL,
        [FileType] nvarchar(100) NULL,
        [DocumentType] nvarchar(20) NOT NULL,
        [UploadedAt] datetime2(0) NOT NULL CONSTRAINT [DF_ContractDocuments_UploadedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ContractDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ContractDocuments_Type] CHECK ((DocumentType IN (N'ContractPage', N'CitizenId', N'Handover', N'Appendix', N'Other'))),
        CONSTRAINT [FK_ContractDocuments_Contracts] FOREIGN KEY ([ContractId]) REFERENCES [dbo].[Contracts] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ContractSettlements] (
        [Id] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [SettlementDate] date NOT NULL,
        [DepositAmount] decimal(18,2) NOT NULL,
        [OutstandingAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_ContractSettlements_Outstanding] DEFAULT (0),
        [DamageFee] decimal(18,2) NOT NULL CONSTRAINT [DF_ContractSettlements_Damage] DEFAULT (0),
        [OtherFee] decimal(18,2) NOT NULL CONSTRAINT [DF_ContractSettlements_Other] DEFAULT (0),
        [RefundAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_ContractSettlements_Refund] DEFAULT (0),
        [Note] nvarchar(1000) NULL,
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_ContractSettlements_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ContractSettlements] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ContractSettlements_Amounts] CHECK ((DepositAmount >= 0 AND OutstandingAmount >= 0 AND DamageFee >= 0 AND OtherFee >= 0 AND RefundAmount >= 0)),
        CONSTRAINT [FK_ContractSettlements_Contracts] FOREIGN KEY ([ContractId]) REFERENCES [dbo].[Contracts] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ContractTenants] (
        [Id] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [JoinDate] date NOT NULL,
        [LeaveDate] date NULL,
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_ContractTenants_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ContractTenants] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ContractTenants_Dates] CHECK ((LeaveDate IS NULL OR LeaveDate >= JoinDate)),
        CONSTRAINT [FK_ContractTenants_Contracts] FOREIGN KEY ([ContractId]) REFERENCES [dbo].[Contracts] ([Id]),
        CONSTRAINT [FK_ContractTenants_Tenants] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[TenantProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Invoices] (
        [Id] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NOT NULL,
        [InvoiceCode] varchar(50) NOT NULL,
        [InvoiceType] nvarchar(20) NOT NULL,
        [BillingYear] smallint NULL,
        [BillingMonth] tinyint NULL,
        [IssueDate] date NOT NULL,
        [DueDate] date NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL CONSTRAINT [DF_Invoices_Status] DEFAULT (N'Draft'),
        [Note] nvarchar(1000) NULL,
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_Invoices_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_Invoices_Id_Contract] UNIQUE ([Id], [ContractId]),
        CONSTRAINT [CK_Invoices_Dates] CHECK ((DueDate >= IssueDate)),
        CONSTRAINT [CK_Invoices_MonthlyPeriod] CHECK ((InvoiceType <> N'Monthly' OR BillingYear IS NOT NULL)),
        CONSTRAINT [CK_Invoices_Period] CHECK (((BillingYear IS NULL AND BillingMonth IS NULL) OR (BillingYear IS NOT NULL AND BillingMonth IS NOT NULL AND BillingYear BETWEEN 2000 AND 2100 AND BillingMonth BETWEEN 1 AND 12))),
        CONSTRAINT [CK_Invoices_Status] CHECK ((Status IN (N'Draft', N'Issued', N'Cancelled'))),
        CONSTRAINT [CK_Invoices_Total] CHECK ((TotalAmount >= 0)),
        CONSTRAINT [CK_Invoices_Type] CHECK ((InvoiceType IN (N'Monthly', N'Deposit', N'Damage', N'Settlement', N'Other'))),
        CONSTRAINT [FK_Invoices_ContractRoom] FOREIGN KEY ([ContractId], [RoomId]) REFERENCES [dbo].[Contracts] ([Id], [RoomId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[DepositApplications] (
        [Id] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [TargetInvoiceId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [IdempotencyKey] varchar(100) NOT NULL,
        [AppliedByUserId] nvarchar(450) NOT NULL,
        [AppliedAt] datetime2(0) NOT NULL CONSTRAINT [DF_DepositApplications_AppliedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_DepositApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DepositApplications_Amount] CHECK ((Amount > 0)),
        CONSTRAINT [FK_DepositApplications_AppliedBy] FOREIGN KEY ([AppliedByUserId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_DepositApplications_Contracts] FOREIGN KEY ([ContractId]) REFERENCES [dbo].[Contracts] ([Id]),
        CONSTRAINT [FK_DepositApplications_TargetInvoice] FOREIGN KEY ([TargetInvoiceId], [ContractId]) REFERENCES [dbo].[Invoices] ([Id], [ContractId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[InvoiceAdjustments] (
        [Id] uniqueidentifier NOT NULL,
        [InvoiceId] uniqueidentifier NOT NULL,
        [AdjustmentType] nvarchar(20) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [IdempotencyKey] varchar(100) NOT NULL,
        [CreatedByUserId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_InvoiceAdjustments_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [VoidedAt] datetime2(0) NULL,
        [VoidReason] nvarchar(500) NULL,
        [VoidedByUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_InvoiceAdjustments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_InvoiceAdjustments_Amount] CHECK ((Amount > 0)),
        CONSTRAINT [CK_InvoiceAdjustments_Type] CHECK ((AdjustmentType IN (N'Discount', N'Correction', N'WriteOff'))),
        CONSTRAINT [CK_InvoiceAdjustments_Void] CHECK (((VoidedAt IS NULL AND VoidReason IS NULL AND VoidedByUserId IS NULL) OR (VoidedAt IS NOT NULL AND VoidReason IS NOT NULL AND VoidedByUserId IS NOT NULL))),
        CONSTRAINT [FK_InvoiceAdjustments_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_InvoiceAdjustments_Invoices] FOREIGN KEY ([InvoiceId]) REFERENCES [dbo].[Invoices] ([Id]),
        CONSTRAINT [FK_InvoiceAdjustments_VoidedBy] FOREIGN KEY ([VoidedByUserId]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[InvoiceItems] (
        [Id] uniqueidentifier NOT NULL,
        [InvoiceId] uniqueidentifier NOT NULL,
        [ItemType] nvarchar(20) NOT NULL,
        [Description] nvarchar(255) NOT NULL,
        [Quantity] decimal(18,2) NOT NULL CONSTRAINT [DF_InvoiceItems_Quantity] DEFAULT (1),
        [UnitPrice] decimal(18,2) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ReferenceId] uniqueidentifier NULL,
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_InvoiceItems_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_InvoiceItems] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_InvoiceItems_Amounts] CHECK ((Quantity > 0 AND UnitPrice >= 0 AND Amount = CASE WHEN ItemType = N'Discount' THEN -CONVERT(decimal(18,2), ROUND(Quantity * UnitPrice, 2)) ELSE CONVERT(decimal(18,2), ROUND(Quantity * UnitPrice, 2)) END)),
        CONSTRAINT [CK_InvoiceItems_Type] CHECK ((ItemType IN (N'Rent', N'Deposit', N'Electricity', N'Water', N'Service', N'Damage', N'Penalty', N'Discount', N'Other'))),
        CONSTRAINT [FK_InvoiceItems_Invoices] FOREIGN KEY ([InvoiceId]) REFERENCES [dbo].[Invoices] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Payments] (
        [Id] uniqueidentifier NOT NULL,
        [InvoiceId] uniqueidentifier NOT NULL,
        [ClaimedAmount] decimal(18,2) NOT NULL,
        [VerifiedAmount] decimal(18,2) NULL,
        [PaymentMethod] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [PaidAt] datetime2(0) NULL,
        [VerifiedAt] datetime2(0) NULL,
        [VerifiedByUserId] nvarchar(450) NULL,
        [TransactionCode] varchar(150) NULL,
        [IdempotencyKey] varchar(100) NOT NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_Payments_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Payments_ClaimedAmount] CHECK ((ClaimedAmount > 0)),
        CONSTRAINT [CK_Payments_Method] CHECK ((PaymentMethod IN (N'Cash', N'BankTransfer', N'OnlinePayment', N'Other'))),
        CONSTRAINT [CK_Payments_Status] CHECK ((Status IN (N'Pending', N'Success', N'Failed', N'Cancelled'))),
        CONSTRAINT [CK_Payments_Verification] CHECK (((Status = N'Success' AND VerifiedAmount IS NOT NULL AND VerifiedAmount > 0 AND PaidAt IS NOT NULL AND VerifiedAt IS NOT NULL AND VerifiedByUserId IS NOT NULL) OR (Status <> N'Success' AND VerifiedAmount IS NULL AND VerifiedAt IS NULL AND VerifiedByUserId IS NULL))),
        CONSTRAINT [FK_Payments_Invoices] FOREIGN KEY ([InvoiceId]) REFERENCES [dbo].[Invoices] ([Id]),
        CONSTRAINT [FK_Payments_VerifiedBy] FOREIGN KEY ([VerifiedByUserId]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Refunds] (
        [Id] uniqueidentifier NOT NULL,
        [InvoiceId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [RefundedAt] datetime2(0) NULL,
        [Reason] nvarchar(500) NOT NULL,
        [TransactionCode] varchar(150) NULL,
        [IdempotencyKey] varchar(100) NOT NULL,
        [CreatedByUserId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2(0) NOT NULL CONSTRAINT [DF_Refunds_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Refunds] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Refunds_Amount] CHECK ((Amount > 0)),
        CONSTRAINT [CK_Refunds_RefundedAt] CHECK ((Status <> N'Success' OR RefundedAt IS NOT NULL)),
        CONSTRAINT [CK_Refunds_Status] CHECK ((Status IN (N'Pending', N'Success', N'Failed', N'Cancelled'))),
        CONSTRAINT [FK_Refunds_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_Refunds_Invoices] FOREIGN KEY ([InvoiceId]) REFERENCES [dbo].[Invoices] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[TenantRequests] (
        [Id] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [RequestedByTenantId] uniqueidentifier NOT NULL,
        [RelatedInvoiceId] uniqueidentifier NULL,
        [Category] nvarchar(20) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Priority] nvarchar(10) NOT NULL CONSTRAINT [DF_TenantRequests_Priority] DEFAULT (N'Medium'),
        [Status] nvarchar(20) NOT NULL CONSTRAINT [DF_TenantRequests_Status] DEFAULT (N'Pending'),
        [AssignedToUserId] nvarchar(450) NULL,
        [RequestedAt] datetime2(0) NOT NULL CONSTRAINT [DF_TenantRequests_RequestedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2(0) NULL,
        [ResolvedAt] datetime2(0) NULL,
        [ResolutionNote] nvarchar(1000) NULL,
        CONSTRAINT [PK_TenantRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TenantRequests_Category] CHECK ((Category IN (N'Repair', N'Billing', N'Question', N'Complaint', N'Other'))),
        CONSTRAINT [CK_TenantRequests_Priority] CHECK ((Priority IN (N'Low', N'Medium', N'High'))),
        CONSTRAINT [CK_TenantRequests_ResolvedAt] CHECK (((Status = N'Resolved' AND ResolvedAt IS NOT NULL) OR (Status <> N'Resolved' AND ResolvedAt IS NULL))),
        CONSTRAINT [CK_TenantRequests_Status] CHECK ((Status IN (N'Pending', N'InProgress', N'Resolved'))),
        CONSTRAINT [FK_TenantRequests_AssignedTo] FOREIGN KEY ([AssignedToUserId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_TenantRequests_Contracts] FOREIGN KEY ([ContractId]) REFERENCES [dbo].[Contracts] ([Id]),
        CONSTRAINT [FK_TenantRequests_Invoices] FOREIGN KEY ([RelatedInvoiceId], [ContractId]) REFERENCES [dbo].[Invoices] ([Id], [ContractId]),
        CONSTRAINT [FK_TenantRequests_Tenants] FOREIGN KEY ([RequestedByTenantId]) REFERENCES [dbo].[TenantProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PaymentReviewEvents] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentId] uniqueidentifier NOT NULL,
        [PreviousStatus] nvarchar(20) NOT NULL,
        [NewStatus] nvarchar(20) NOT NULL,
        [PreviousVerifiedAmount] decimal(18,2) NULL,
        [NewVerifiedAmount] decimal(18,2) NULL,
        [Reason] nvarchar(500) NOT NULL,
        [ReviewedByUserId] nvarchar(450) NOT NULL,
        [ReviewedAt] datetime2(0) NOT NULL CONSTRAINT [DF_PaymentReviewEvents_ReviewedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_PaymentReviewEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PaymentReviewEvents_Amounts] CHECK (((PreviousVerifiedAmount IS NULL OR PreviousVerifiedAmount > 0) AND (NewVerifiedAmount IS NULL OR NewVerifiedAmount > 0))),
        CONSTRAINT [CK_PaymentReviewEvents_NewStatus] CHECK ((NewStatus IN (N'Pending', N'Success', N'Failed', N'Cancelled'))),
        CONSTRAINT [CK_PaymentReviewEvents_PreviousStatus] CHECK ((PreviousStatus IN (N'Pending', N'Success', N'Failed', N'Cancelled'))),
        CONSTRAINT [FK_PaymentReviewEvents_Payments] FOREIGN KEY ([PaymentId]) REFERENCES [dbo].[Payments] ([Id]),
        CONSTRAINT [FK_PaymentReviewEvents_ReviewedBy] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ContractDocuments_ContractId] ON [dbo].[ContractDocuments] ([ContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Contracts_Room_Dates] ON [dbo].[Contracts] ([RoomId], [StartDate], [EndDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Contracts_Code] ON [dbo].[Contracts] ([ContractCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Contracts_OneActivePerRoom] ON [dbo].[Contracts] ([RoomId]) WHERE Status = N''Active''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ContractSettlements_ContractId] ON [dbo].[ContractSettlements] ([ContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ContractTenants_TenantId] ON [dbo].[ContractTenants] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ContractTenants_Join] ON [dbo].[ContractTenants] ([ContractId], [TenantId], [JoinDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_DepositApplications_AppliedByUserId] ON [dbo].[DepositApplications] ([AppliedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_DepositApplications_ContractId] ON [dbo].[DepositApplications] ([ContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_DepositApplications_TargetInvoiceId] ON [dbo].[DepositApplications] ([TargetInvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_DepositApplications_TargetInvoiceId_ContractId] ON [dbo].[DepositApplications] ([TargetInvoiceId], [ContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_DepositApplications_IdempotencyKey] ON [dbo].[DepositApplications] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_InvoiceAdjustments_CreatedByUserId] ON [dbo].[InvoiceAdjustments] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_InvoiceAdjustments_InvoiceId] ON [dbo].[InvoiceAdjustments] ([InvoiceId], [VoidedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_InvoiceAdjustments_VoidedByUserId] ON [dbo].[InvoiceAdjustments] ([VoidedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_InvoiceAdjustments_IdempotencyKey] ON [dbo].[InvoiceAdjustments] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_InvoiceItems_InvoiceId] ON [dbo].[InvoiceItems] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Invoices_Contract_Status] ON [dbo].[Invoices] ([ContractId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Invoices_ContractId_RoomId] ON [dbo].[Invoices] ([ContractId], [RoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Invoices_Code] ON [dbo].[Invoices] ([InvoiceCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Invoices_Deposit] ON [dbo].[Invoices] ([ContractId]) WHERE InvoiceType = N''Deposit'' AND Status <> N''Cancelled''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Invoices_Monthly] ON [dbo].[Invoices] ([ContractId], [BillingYear], [BillingMonth]) WHERE InvoiceType = N''Monthly'' AND Status <> N''Cancelled''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_MeterReadings_Period] ON [dbo].[MeterReadings] ([RoomId], [MeterType], [Year], [Month]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PaymentReviewEvents_Payment_ReviewedAt] ON [dbo].[PaymentReviewEvents] ([PaymentId], [ReviewedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PaymentReviewEvents_ReviewedByUserId] ON [dbo].[PaymentReviewEvents] ([ReviewedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Payments_Invoice_Status] ON [dbo].[Payments] ([InvoiceId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Payments_VerifiedByUserId] ON [dbo].[Payments] ([VerifiedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Payments_IdempotencyKey] ON [dbo].[Payments] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Payments_TransactionCode] ON [dbo].[Payments] ([TransactionCode]) WHERE TransactionCode IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Properties_OwnerUserId] ON [dbo].[Properties] ([OwnerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Refunds_CreatedByUserId] ON [dbo].[Refunds] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Refunds_Invoice_Status] ON [dbo].[Refunds] ([InvoiceId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Refunds_IdempotencyKey] ON [dbo].[Refunds] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Refunds_TransactionCode] ON [dbo].[Refunds] ([TransactionCode]) WHERE TransactionCode IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Rooms_Property_Status] ON [dbo].[Rooms] ([PropertyId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Rooms_Property_Number] ON [dbo].[Rooms] ([PropertyId], [RoomNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_RoomServices_ServiceId] ON [dbo].[RoomServices] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_RoomServices_Open] ON [dbo].[RoomServices] ([RoomId], [ServiceId]) WHERE EndDate IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_RoomServices_Start] ON [dbo].[RoomServices] ([RoomId], [ServiceId], [StartDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Services_Name] ON [dbo].[Services] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TenantProfiles_CitizenId] ON [dbo].[TenantProfiles] ([CitizenId]) WHERE CitizenId IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TenantProfiles_UserId] ON [dbo].[TenantProfiles] ([UserId]) WHERE UserId IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TenantRequests_Assigned_Status] ON [dbo].[TenantRequests] ([AssignedToUserId], [Status]) WHERE AssignedToUserId IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_TenantRequests_Contract_Status] ON [dbo].[TenantRequests] ([ContractId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_TenantRequests_RelatedInvoiceId_ContractId] ON [dbo].[TenantRequests] ([RelatedInvoiceId], [ContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_TenantRequests_Tenant_Status] ON [dbo].[TenantRequests] ([RequestedByTenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Users_NormalizedEmail] ON [dbo].[Users] ([NormalizedEmail]) WHERE NormalizedEmail IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Users_NormalizedUserName] ON [dbo].[Users] ([NormalizedUserName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_UtilityRates_Open] ON [dbo].[UtilityRates] ([PropertyId], [UtilityType]) WHERE EffectiveTo IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_UtilityRates_Start] ON [dbo].[UtilityRates] ([PropertyId], [UtilityType], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    EXEC(N'CREATE VIEW dbo.vwInvoiceBalances AS
    SELECT i.Id AS InvoiceId, i.ContractId, i.RoomId, i.InvoiceType, i.Status,
           i.TotalAmount,
           CONVERT(decimal(18,2), COALESCE(a.TotalAdjustments, 0)) AS TotalAdjustments,
           CONVERT(decimal(18,2), COALESCE(p.TotalPaid, 0)) AS TotalPaid,
           CONVERT(decimal(18,2), COALESCE(r.TotalRefunded, 0)) AS TotalRefunded,
           CONVERT(decimal(18,2), COALESCE(d.TotalDepositApplied, 0)) AS TotalDepositApplied,
           CASE WHEN i.Status <> N''Issued'' THEN CONVERT(decimal(18,2), 0)
                ELSE i.TotalAmount - CONVERT(decimal(18,2), COALESCE(a.TotalAdjustments, 0))
           END AS EffectiveAmount,
           CASE WHEN i.Status <> N''Issued'' THEN CONVERT(decimal(18,2), 0)
                ELSE i.TotalAmount - CONVERT(decimal(18,2), COALESCE(a.TotalAdjustments, 0))
           END - CONVERT(decimal(18,2), COALESCE(p.TotalPaid, 0))
               + CASE WHEN i.InvoiceType = N''Deposit'' THEN CONVERT(decimal(18,2), 0)
                      ELSE CONVERT(decimal(18,2), COALESCE(r.TotalRefunded, 0)) END
               - CONVERT(decimal(18,2), COALESCE(d.TotalDepositApplied, 0)) AS RemainingAmount
    FROM dbo.Invoices AS i
    OUTER APPLY (
        SELECT SUM(CONVERT(decimal(38,2), a.Amount)) AS TotalAdjustments
        FROM dbo.InvoiceAdjustments AS a
        WHERE a.InvoiceId = i.Id AND a.VoidedAt IS NULL
    ) AS a
    OUTER APPLY (
        SELECT SUM(CONVERT(decimal(38,2), p.VerifiedAmount)) AS TotalPaid
        FROM dbo.Payments AS p
        WHERE p.InvoiceId = i.Id AND p.Status = N''Success''
    ) AS p
    OUTER APPLY (
        SELECT SUM(CONVERT(decimal(38,2), r.Amount)) AS TotalRefunded
        FROM dbo.Refunds AS r
        WHERE r.InvoiceId = i.Id AND r.Status = N''Success''
    ) AS r
    OUTER APPLY (
        SELECT SUM(CONVERT(decimal(38,2), d.Amount)) AS TotalDepositApplied
        FROM dbo.DepositApplications AS d
        WHERE d.TargetInvoiceId = i.Id
    ) AS d;')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    EXEC(N'CREATE VIEW dbo.vwContractDepositBalances AS
    SELECT i.ContractId, i.Id AS DepositInvoiceId,
           b.TotalPaid AS DepositCollected,
           b.TotalRefunded AS DepositRefunded,
           CONVERT(decimal(18,2), COALESCE(d.TotalApplied, 0)) AS DepositApplied,
           b.TotalPaid - b.TotalRefunded - CONVERT(decimal(18,2), COALESCE(d.TotalApplied, 0)) AS DepositHeld
    FROM dbo.Invoices AS i
    JOIN dbo.vwInvoiceBalances AS b ON b.InvoiceId = i.Id
    OUTER APPLY (
        SELECT SUM(CONVERT(decimal(38,2), d.Amount)) AS TotalApplied
        FROM dbo.DepositApplications AS d
        WHERE d.ContractId = i.ContractId
    ) AS d
    WHERE i.InvoiceType = N''Deposit'' AND i.Status = N''Issued'';')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022052413_InitialSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022052413_InitialSchema', N'10.0.12');
END;

COMMIT;
GO

