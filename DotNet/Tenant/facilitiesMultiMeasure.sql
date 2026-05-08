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
    WHERE [MigrationId] = N'20240723171206_AddFacilitiesTable'
)
BEGIN
    CREATE TABLE [Facilities] (
        [Id] uniqueidentifier NOT NULL,
        [FacilityId] nvarchar(max) NOT NULL,
        [FacilityName] nvarchar(max) NULL,
        [MRPModifyDate] datetime2 NULL,
        [MRPCreatedDate] datetime2 NULL,
        [CreateDate] datetime2 NOT NULL,
        [ModifyDate] datetime2 NULL,
        [MonthlyReportingPlans] nvarchar(max) NULL,
        [ScheduledTasks] nvarchar(max) NULL,
        CONSTRAINT [PK_Facilities] PRIMARY KEY NONCLUSTERED ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240723171206_AddFacilitiesTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240723171206_AddFacilitiesTable', N'8.0.3');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240911180606_MultiMeasureChanges'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Facilities]') AND [c].[name] = N'MRPCreatedDate');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Facilities] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Facilities] DROP COLUMN [MRPCreatedDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240911180606_MultiMeasureChanges'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Facilities]') AND [c].[name] = N'MRPModifyDate');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Facilities] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Facilities] DROP COLUMN [MRPModifyDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240911180606_MultiMeasureChanges'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Facilities]') AND [c].[name] = N'MonthlyReportingPlans');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Facilities] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Facilities] DROP COLUMN [MonthlyReportingPlans];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240911180606_MultiMeasureChanges'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Facilities]') AND [c].[name] = N'ScheduledTasks');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Facilities] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Facilities] DROP COLUMN [ScheduledTasks];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240911180606_MultiMeasureChanges'
)
BEGIN
    ALTER TABLE [Facilities] ADD [ScheduledReports] nvarchar(max) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240911180606_MultiMeasureChanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240911180606_MultiMeasureChanges', N'8.0.3');
END;
GO

COMMIT;
GO

