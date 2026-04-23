using Xunit;

// Integration test fixtures in this assembly each spin up their own Microsoft.Extensions.Hosting IHost
// (Tenant, Census, DataAcquisition, Report, Normalization). Those hosts touch process-wide static
// state (Quartz LogProvider / SchedulerRepository, OpenTelemetry global TracerProvider, etc.). Allowing
// xUnit's default cross-collection parallelism in this single process produces intermittent
// ObjectDisposedException: 'LoggerFactory' failures when one fixture's host disposes while another
// collection is still executing. Disabling parallelization keeps the within-collection ordering
// xUnit already enforced and serializes execution between collections.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
