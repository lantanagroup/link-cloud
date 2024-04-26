CREATE TABLE [NormalizationConfig] (
    [Id] int NOT NULL IDENTITY,
    [FacilityId] varchar(max) NULL,
    [OperationSequence] nvarchar(max) NULL,
    [CreatedDate] datetime2 NULL DEFAULT ((getutcdate())),
    [ModifiedDate] datetime2 NULL,
    CONSTRAINT [PK_NormalizationConfig] PRIMARY KEY ([Id])
);
GO