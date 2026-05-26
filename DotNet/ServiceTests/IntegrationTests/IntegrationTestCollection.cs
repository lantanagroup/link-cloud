using IntegrationTests.AutomationUI;
using IntegrationTests.Census;
using IntegrationTests.DataAcquisition;
using IntegrationTests.Normalization;
using IntegrationTests.Report;
using IntegrationTests.Tenant;

namespace IntegrationTests;

/// <summary>
/// Single master collection that owns every integration-test fixture in the assembly.
///
/// All <c>[Collection("IntegrationTests")]</c>-annotated test classes share this collection,
/// which xUnit guarantees to execute serially on a single thread (within-collection
/// behavior). That means no two integration-test fixtures' <c>IHost</c>s are ever
/// "active" against tests at the same time, eliminating the cross-fixture
/// LoggerFactory / Quartz / OpenTelemetry races we saw when each service had its own
/// collection (multiple collections still run in parallel by default in xUnit, even
/// with <c>DisableParallelization = true</c> on each individual collection).
///
/// Unit tests are NOT in this collection (they have no <c>[Collection]</c> attribute),
/// so xUnit continues to parallelize them across the assembly's parallel queue as usual.
///
/// xUnit instantiates each <see cref="ICollectionFixture{T}"/> lazily on first use and
/// disposes them once at the end of the entire collection's run, so adding new fixtures
/// here costs nothing for tests that don't touch them.
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationTestCollection :
    ICollectionFixture<TenantIntegrationTestFixture>,
    ICollectionFixture<CensusIntegrationTestFixture>,
    ICollectionFixture<DataAcquisitionIntegrationTestFixture>,
    ICollectionFixture<NormalizationIntegrationTestFixture>,
    ICollectionFixture<ReportIntegrationTestFixture>,
    ICollectionFixture<AutomationUIIntegrationTestFixture>
{
    public const string Name = "IntegrationTests";
}
