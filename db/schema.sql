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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE TABLE [Practices] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_Practices] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE TABLE [PaymentRuns] (
        [Id] int NOT NULL IDENTITY,
        [PublicId] uniqueidentifier NOT NULL,
        [PracticeId] int NOT NULL,
        [PeriodStartUtc] datetime2 NOT NULL,
        [PeriodEndUtc] datetime2 NOT NULL,
        [FeePercentage] decimal(9,4) NOT NULL,
        [FixedFeePerTimesheet] decimal(18,2) NOT NULL,
        [BusinessReference] nvarchar(100) NOT NULL,
        [Currency] nchar(3) NOT NULL,
        [GrossTotal] decimal(18,2) NOT NULL,
        [FeeTotal] decimal(18,2) NOT NULL,
        [NetTotal] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentRuns_Practices_PracticeId] FOREIGN KEY ([PracticeId]) REFERENCES [Practices] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [PublicId] uniqueidentifier NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [RoleId] int NOT NULL,
        [PracticeId] int NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Practices_PracticeId] FOREIGN KEY ([PracticeId]) REFERENCES [Practices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Users_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE TABLE [Shifts] (
        [Id] int NOT NULL IDENTITY,
        [PracticeId] int NOT NULL,
        [ClinicianId] int NULL,
        [Date] date NOT NULL,
        [StartUtc] datetime2 NOT NULL,
        [EndUtc] datetime2 NOT NULL,
        [HourlyRate] decimal(18,2) NOT NULL,
        [Role] nvarchar(100) NOT NULL,
        [Location] nvarchar(200) NOT NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Shifts_Practices_PracticeId] FOREIGN KEY ([PracticeId]) REFERENCES [Practices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Shifts_Users_ClinicianId] FOREIGN KEY ([ClinicianId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE TABLE [Timesheets] (
        [Id] int NOT NULL IDENTITY,
        [PublicId] uniqueidentifier NOT NULL,
        [ShiftId] int NOT NULL,
        [ClinicianId] int NOT NULL,
        [WorkedStartUtc] datetime2 NOT NULL,
        [WorkedEndUtc] datetime2 NOT NULL,
        [UnpaidBreakMinutes] int NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [BusinessReference] nvarchar(100) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Timesheets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Timesheets_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Timesheets_Users_ClinicianId] FOREIGN KEY ([ClinicianId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE TABLE [PaymentRunLineItems] (
        [Id] int NOT NULL IDENTITY,
        [PaymentRunId] int NOT NULL,
        [TimesheetId] int NOT NULL,
        [ClinicianId] int NOT NULL,
        [Hours] decimal(9,2) NOT NULL,
        [Rate] decimal(18,2) NOT NULL,
        [Gross] decimal(18,2) NOT NULL,
        [Fee] decimal(18,2) NOT NULL,
        [Net] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_PaymentRunLineItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentRunLineItems_PaymentRuns_PaymentRunId] FOREIGN KEY ([PaymentRunId]) REFERENCES [PaymentRuns] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentRunLineItems_Timesheets_TimesheetId] FOREIGN KEY ([TimesheetId]) REFERENCES [Timesheets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentRunLineItems_Users_ClinicianId] FOREIGN KEY ([ClinicianId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentRunLineItems_ClinicianId] ON [PaymentRunLineItems] ([ClinicianId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentRunLineItems_PaymentRunId] ON [PaymentRunLineItems] ([PaymentRunId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentRunLineItems_TimesheetId] ON [PaymentRunLineItems] ([TimesheetId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentRuns_BusinessReference] ON [PaymentRuns] ([BusinessReference]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentRuns_PracticeId] ON [PaymentRuns] ([PracticeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentRuns_PublicId] ON [PaymentRuns] ([PublicId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shifts_ClinicianId] ON [Shifts] ([ClinicianId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shifts_PracticeId] ON [Shifts] ([PracticeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Timesheets_BusinessReference] ON [Timesheets] ([BusinessReference]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Timesheets_ClinicianId] ON [Timesheets] ([ClinicianId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Timesheets_PublicId] ON [Timesheets] ([PublicId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Timesheets_ShiftId] ON [Timesheets] ([ShiftId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_PracticeId] ON [Users] ([PracticeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_PublicId] ON [Users] ([PublicId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_RoleId] ON [Users] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715144628_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260715144628_InitialCreate', N'8.0.8');
END;
GO

COMMIT;
GO

