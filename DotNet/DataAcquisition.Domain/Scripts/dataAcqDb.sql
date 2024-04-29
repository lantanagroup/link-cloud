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
    WHERE [MigrationId] = N'20240424184458_Init'
)
BEGIN
    CREATE TABLE [fhirListConfiguration] (
        [Id] uniqueidentifier NOT NULL,
        [FacilityId] nvarchar(max) NOT NULL,
        [FhirBaseServerUrl] nvarchar(max) NOT NULL,
        [Authentication] nvarchar(max) NULL,
        [EHRPatientLists] nvarchar(max) NOT NULL,
        [CreateDate] datetime2 NOT NULL,
        [ModifyDate] datetime2 NULL,
        CONSTRAINT [PK_fhirListConfiguration] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240424184458_Init'
)
BEGIN
    CREATE TABLE [fhirQueryConfiguration] (
        [Id] uniqueidentifier NOT NULL,
        [FacilityId] nvarchar(max) NOT NULL,
        [FhirServerBaseUrl] nvarchar(max) NOT NULL,
        [Authentication] nvarchar(max) NULL,
        [QueryPlanIds] nvarchar(max) NULL,
        [CreateDate] datetime2 NOT NULL,
        [ModifyDate] datetime2 NULL,
        CONSTRAINT [PK_fhirQueryConfiguration] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240424184458_Init'
)
BEGIN
    CREATE TABLE [queriedFhirResource] (
        [Id] uniqueidentifier NOT NULL,
        [FacilityId] nvarchar(max) NOT NULL,
        [CorrelationId] nvarchar(max) NOT NULL,
        [PatientId] nvarchar(max) NOT NULL,
        [QueryType] nvarchar(max) NOT NULL,
        [ResourceType] nvarchar(max) NOT NULL,
        [ResourceId] nvarchar(max) NOT NULL,
        [IsSuccessful] bit NOT NULL,
        [CreateDate] datetime2 NULL,
        [ModifyDate] datetime2 NULL,
        CONSTRAINT [PK_queriedFhirResource] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240424184458_Init'
)
BEGIN
    CREATE TABLE [queryPlan] (
        [Id] uniqueidentifier NOT NULL,
        [PlanName] nvarchar(max) NOT NULL,
        [ReportType] nvarchar(max) NOT NULL,
        [FacilityId] nvarchar(max) NOT NULL,
        [EHRDescription] nvarchar(max) NOT NULL,
        [LookBack] nvarchar(max) NOT NULL,
        [InitialQueries] nvarchar(max) NOT NULL,
        [SupplementalQueries] nvarchar(max) NOT NULL,
        [CreateDate] datetime2 NOT NULL,
        [ModifyDate] datetime2 NULL,
        CONSTRAINT [PK_queryPlan] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240424184458_Init'
)
BEGIN
    CREATE TABLE [referenceResources] (
        [Id] uniqueidentifier NOT NULL,
        [FacilityId] nvarchar(max) NOT NULL,
        [ResourceId] nvarchar(max) NOT NULL,
        [ResourceType] nvarchar(max) NOT NULL,
        [ReferenceResource] nvarchar(max) NOT NULL,
        [CreateDate] datetime2 NOT NULL,
        [ModifyDate] datetime2 NULL,
        CONSTRAINT [PK_referenceResources] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240424184458_Init'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240424184458_Init', N'8.0.4');
END;
GO

COMMIT;
GO

