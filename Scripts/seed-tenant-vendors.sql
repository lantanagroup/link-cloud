-- SQL Server: run against the intended tenant database (normally link-tenant).
-- Source: https://fhir-api.luxera.io/api/v1/vendors
-- Retrieved: 2026-10-19. All 30 vendor names are preserved as returned.
-- Requires the tenant Vendor and VendorVersion migrations to be applied.
-- Existing vendors are matched by Name using the database collation.
-- Existing vendors, authentication settings, and versions are preserved.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Vendors TABLE ([Name] varchar(255) NOT NULL PRIMARY KEY);

    INSERT INTO @Vendors ([Name])
    VALUES
        ('AdvancedMD'),
        ('athenahealth'),
        ('Benchmark Systems (Prognocis)'),
        ('Bizmatics'),
        ('Canvas Medical'),
        ('Carepaths'),
        ('CareTracker'),
        ('CureMD'),
        ('Darena Solutions'),
        ('eClinicalWorks'),
        ('Elation Health'),
        ('Epic Systems'),
        ('Eyefinity'),
        ('EyeMD EMR'),
        ('First Insight'),
        ('Flatiron Health'),
        ('Greenway Health'),
        ('Harris CareTracker'),
        ('iSALUS Healthcare'),
        ('MEDHOST'),
        ('MEDITECH'),
        ('Modernizing Medicine'),
        ('Netsmart Technologies'),
        ('Office Ally'),
        ('Office Practicum'),
        ('Oracle Health (Cerner)'),
        ('PointClickCare'),
        ('Practice Fusion'),
        ('Streamline Healthcare Solutions'),
        ('Veradigm/Allscripts');

    -- Reject ambiguous existing names rather than choosing an arbitrary vendor.
    IF EXISTS (
        SELECT v.[Name]
        FROM [dbo].[Vendor] v WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN @Vendors source ON source.[Name] = v.[Name]
        GROUP BY v.[Name]
        HAVING COUNT(*) > 1
    )
        THROW 50001, 'Multiple existing vendors share a source vendor name. Resolve duplicates before rerunning.', 1;

    INSERT INTO [dbo].[Vendor] ([Id], [Name])
    SELECT NEWID(), source.[Name]
    FROM @Vendors source
    WHERE NOT EXISTS (
        SELECT 1
        FROM [dbo].[Vendor] v WITH (UPDLOCK, HOLDLOCK)
        WHERE v.[Name] = source.[Name]
    );

    DECLARE @VendorsInserted int = @@ROWCOUNT;

    INSERT INTO [dbo].[VendorVersion] ([Id], [VendorId], [Version])
    SELECT NEWID(), v.[Id], 'default'
    FROM [dbo].[Vendor] v
    INNER JOIN @Vendors source ON source.[Name] = v.[Name]
    WHERE NOT EXISTS (
        SELECT 1
        FROM [dbo].[VendorVersion] vv WITH (UPDLOCK, HOLDLOCK)
        WHERE vv.[VendorId] = v.[Id] AND vv.[Version] = 'default'
    );

    DECLARE @VersionsInserted int = @@ROWCOUNT;

    COMMIT TRANSACTION;

    SELECT @VendorsInserted AS VendorsInserted,
           @VersionsInserted AS DefaultVersionsInserted;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
