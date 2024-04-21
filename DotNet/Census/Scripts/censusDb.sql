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
    WHERE [MigrationId] = N'20240409145111_Init'
)
BEGIN
    CREATE TABLE [CensusConfig] (
        [Id] uniqueidentifier NOT NULL,
        [FacilityID] nvarchar(max) NOT NULL,
        [ScheduledTrigger] nvarchar(max) NOT NULL,
        [CreateDate] datetime2 NOT NULL,
        [ModifyDate] datetime2 NOT NULL,
        CONSTRAINT [PK_CensusConfig] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240409145111_Init'
)
BEGIN
    CREATE TABLE [CensusPatientList] (
        [Id] uniqueidentifier NOT NULL,
        [FacilityId] nvarchar(max) NOT NULL,
        [PatientId] nvarchar(max) NOT NULL,
        [DisplayName] nvarchar(max) NOT NULL,
        [AdmitDate] datetime2 NOT NULL,
        [IsDischarged] bit NOT NULL,
        [DischargeDate] datetime2 NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NOT NULL,
        [CreateDate] datetime2 NOT NULL,
        [ModifyDate] datetime2 NULL,
        CONSTRAINT [PK_CensusPatientList] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240409145111_Init'
)
BEGIN
    CREATE TABLE [PatientCensusHistory] (
        [Id] uniqueidentifier NOT NULL,
        [FacilityId] nvarchar(max) NOT NULL,
        [CensusDateTime] datetime2 NOT NULL,
        [ReportId] nvarchar(max) NOT NULL,
        [CreateDate] datetime2 NOT NULL,
        [ModifyDate] datetime2 NULL,
        CONSTRAINT [PK_PatientCensusHistory] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240409145111_Init'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240409145111_Init', N'8.0.3');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240417172806_DisplayNameNull'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CensusPatientList]') AND [c].[name] = N'CreatedDate');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [CensusPatientList] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [CensusPatientList] DROP COLUMN [CreatedDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240417172806_DisplayNameNull'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CensusPatientList]') AND [c].[name] = N'UpdatedDate');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [CensusPatientList] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [CensusPatientList] DROP COLUMN [UpdatedDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240417172806_DisplayNameNull'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CensusPatientList]') AND [c].[name] = N'DisplayName');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [CensusPatientList] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [CensusPatientList] ALTER COLUMN [DisplayName] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240417172806_DisplayNameNull'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CensusPatientList]') AND [c].[name] = N'DischargeDate');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [CensusPatientList] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [CensusPatientList] ALTER COLUMN [DischargeDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240417172806_DisplayNameNull'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CensusPatientList]') AND [c].[name] = N'AdmitDate');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [CensusPatientList] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [CensusPatientList] ALTER COLUMN [AdmitDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240417172806_DisplayNameNull'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CensusConfig]') AND [c].[name] = N'ModifyDate');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [CensusConfig] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [CensusConfig] ALTER COLUMN [ModifyDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240417172806_DisplayNameNull'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240417172806_DisplayNameNull', N'8.0.3');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240419191012_ComputedColumnPatientHistoryReportId'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PatientCensusHistory]') AND [c].[name] = N'ReportId');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [PatientCensusHistory] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [PatientCensusHistory] DROP COLUMN [ReportId];
    EXEC(N'ALTER TABLE [PatientCensusHistory] ADD [ReportId] AS CONCAT(FacilityId, ''-'', CensusDateTime)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240419191012_ComputedColumnPatientHistoryReportId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240419191012_ComputedColumnPatientHistoryReportId', N'8.0.3');
END;
GO

COMMIT;
GO

