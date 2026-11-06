namespace Link.AspireAppHost;

/// <summary>
/// Fixed host ports for everything the AppHost starts.
/// </summary>
/// <remarks>
/// They are fixed so that URLs stay the same between runs and tools run from a terminal can find
/// them. Every one differs from the ports docker-compose uses, so both stacks can run at once: each
/// service is its compose port plus 10000, and each container avoids the usual default (1433, 9092,
/// 27017, 6379, 10000, 3100), which another stack on the machine often already holds.
/// </remarks>
internal static class Ports
{
    // Infrastructure
    public const int SqlServer = 14333;
    public const int Kafka = 19092;
    public const int KafkaUi = 19080;
    public const int KafkaRestProxy = 18082;
    public const int Mongo = 37017;
    public const int Redis = 16379;
    public const int AzuriteBlob = 11000;
    public const int Loki = 13100;
    public const int HapiFhir = 16157;
    public const int Keycloak = 18443;

    // .NET services
    public const int Account = 18060;
    public const int Audit = 18062;
    public const int AdminBff = 18063;
    public const int Census = 18064;
    public const int DataAcquisition = 18065;
    public const int Normalization = 18068;
    public const int QueryDispatch = 18071;
    public const int Report = 18072;
    public const int Submission = 18073;
    public const int Tenant = 18074;
    public const int Terminology = 18076;
    public const int AcquisitionWorker = 18078;
    public const int MockFhirServer = 16158;
    public const int MockDmrpApi = 16159;
    public const int AutomationUi = 15256;

    // Java services
    public const int MeasureEval = 18067;
    public const int Validation = 18075;

    // Admin UI under ng serve
    public const int AdminUi = 14200;
}
