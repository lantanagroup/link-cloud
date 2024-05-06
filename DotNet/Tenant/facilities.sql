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
    WHERE [MigrationId] = N'20240403135254_AddFacilitiesTable'
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
    WHERE [MigrationId] = N'20240403135254_AddFacilitiesTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240403135254_AddFacilitiesTable', N'8.0.3');
END;
GO

COMMIT;
GO

