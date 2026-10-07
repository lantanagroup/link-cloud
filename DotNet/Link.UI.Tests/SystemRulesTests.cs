using FluentAssertions;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class SystemRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void User_fields_roles_and_the_search_query_are_checked()
    {
        SystemRules.CheckUser(new UserForm { Username = "ada", FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" }, ["Reader"], out var created)
            .Should().BeNull();
        created.Email.Should().Be("ada@example.com");
        created.Roles.Should().BeEmpty();

        SystemRules.CheckUser(new UserForm { Username = "bad name", FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" }, [], out _)
            .Should().Contain("Username");
        SystemRules.CheckUser(new UserForm { Username = "ada", FirstName = "Ada", LastName = "Lovelace", Email = "not-an-email" }, [], out _)
            .Should().Contain("Email");
        SystemRules.CheckUser(new UserForm
        {
            Username = "ada",
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Roles = ["missing"]
        }, ["Reader"], out _).Should().Contain("already exists");

        SystemRules.CheckUser(new UserForm
        {
            Username = "ada",
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Roles = ["reader"]
        }, ["Reader"], out var cased).Should().BeNull();
        cased.Roles.Should().Equal("Reader");

        SystemRules.CheckUserQuery(new UserQuery { FacilityId = "!!!", Page = 0, PageSize = 500 }, numericOnly: false, out var query)
            .Should().NotBeNull();
        SystemRules.CheckUserQuery(new UserQuery { SearchText = "ada", Page = 2, PageSize = 20, IncludeDeleted = true }, numericOnly: false, out var clean)
            .Should().BeNull();
        clean.Page.Should().Be(2);
        clean.PageSize.Should().Be(20);
        clean.IncludeDeleted.Should().BeTrue();
        query.Page.Should().Be(1);
    }

    [Fact]
    public void Saving_a_user_or_role_keeps_the_claims_already_stored()
    {
        var merged = SystemRules.MergeUser(
            new AccountUserApiModel { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), UserClaims = ["CanViewLogs"], IsActive = false },
            new AccountUserInput { Username = "ada", FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com", Roles = ["Reader"] });
        merged.UserClaims.Should().Equal("CanViewLogs");
        merged.IsActive.Should().BeFalse();
        merged.Roles.Should().Equal("Reader");

        var role = SystemRules.MergeRole(
            new AccountRoleApiModel { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Claims = ["CanViewLogs"] },
            "Reader",
            "Reads");
        role.Claims.Should().Equal("CanViewLogs");
        role.Name.Should().Be("Reader");

        SystemRules.CheckRole(new RoleForm { Name = " ", Description = "Reads" }, out _, out _).Should().Contain("Name");
        SystemRules.CheckRole(new RoleForm { Name = "Reader", Description = new string('d', 1001) }, out _, out _).Should().Contain("Description");
        SystemRules.CheckUserId("not-a-guid", out _).Should().Contain("GUID");
    }

    [Fact]
    public void Health_service_names_reports_and_versions_are_read()
    {
        SystemRules.CheckHealthService("Measure Eval", out var key).Should().BeNull();
        key.Should().Be("measureeval");
        SystemRules.CheckHealthService("nope", out _).Should().Contain("Service");
        SystemRules.CheckHealthService(" ", out var blank).Should().BeNull();
        blank.Should().BeNull();

        var health = """
            [{"service":"Account","status":"Healthy","totalDuration":"00:00:00.012","entries":{"db":{"status":"Healthy","duration":"00:00:00.002","description":"ok"}}}]
            """;
        SystemRules.ReadHealth(health, out var rows).Should().BeNull();
        rows.Should().ContainSingle();
        rows[0].Service.Should().Be("Account");
        rows[0].Duration.Should().Be("12 ms");
        rows[0].Entries.Should().ContainSingle();
        rows[0].Entries[0].Name.Should().Be("db");

        SystemRules.ReadHealth("{\"service\":\"Account\",\"status\":\"Degraded\",\"totalDuration\":\"00:00:02\"}", out var one).Should().BeNull();
        one.Should().ContainSingle();
        one[0].Status.Should().Be("Degraded");
        one[0].Duration.Should().Be("2 s");
        SystemRules.ReadHealth("{", out _).Should().Contain("JSON");
        SystemRules.ReadServiceInfo("[{\"serviceName\":\"Account\",\"version\":\"1\",\"commit\":\"" + new string('a', 50) + "\"}]", out var info)
            .Should().BeNull();
        info[0].Commit.Should().HaveLength(40);
        SystemRules.FormatDuration("").Should().BeEmpty();
        SystemRules.FormatDuration("later").Should().Be("later");
    }

    [Fact]
    public void App_configuration_shows_http_addresses_and_drops_secrets()
    {
        var (rows, missing) = SystemRules.ReadAddresses(new ServiceRegistry
        {
            AccountServiceUrl = "http://user:pass@localhost:8060/api/",
            AdminBffServiceUrl = "http://localhost:8063",
            TenantService = new TenantServiceRegistration { TenantServiceUrl = "not a url and secret=value" }
        });

        rows.Should().Contain(row => row.Label == "Account" && row.Url == "http://localhost:8060/api");
        rows.Should().Contain(row => row.Label == "Admin.BFF" && row.Url == "http://localhost:8063");
        rows.Should().NotContain(row => row.Label == "Tenant");
        missing.Should().Contain("Tenant");
        missing.Should().Contain("Notification");
        missing.Should().NotContain("Account");
    }

    [Fact]
    public void Integration_events_reject_a_future_date_a_bad_frequency_and_an_empty_patient_list()
    {
        SystemRules.CheckReportScheduled(new ReportScheduledForm
        {
            FacilityId = "!!!",
            Frequency = "Daily",
            ReportTypes = "NHSN",
            StartDate = "2026-10-01",
            DelayMinutes = "15"
        }, numericOnly: false, Now, out _).Should().NotBeNull();

        SystemRules.CheckReportScheduled(new ReportScheduledForm
        {
            FacilityId = "link-ui",
            Frequency = "Yearly",
            ReportTypes = "NHSN",
            StartDate = "2026-10-01",
            DelayMinutes = "15"
        }, numericOnly: false, Now, out _).Should().Contain("Frequency");

        SystemRules.CheckReportScheduled(new ReportScheduledForm
        {
            FacilityId = "link-ui",
            Frequency = "Daily",
            ReportTypes = "NHSN",
            StartDate = "2026-10-08",
            DelayMinutes = "15"
        }, numericOnly: false, Now, out _).Should().Contain("past");

        SystemRules.CheckReportScheduled(new ReportScheduledForm
        {
            FacilityId = "link-ui",
            Frequency = "Daily",
            ReportTypes = "NHSN, NHSN",
            StartDate = "2026-10-01",
            DelayMinutes = "nope"
        }, numericOnly: false, Now, out _).Should().Contain("Delay");

        SystemRules.CheckReportScheduled(new ReportScheduledForm
        {
            FacilityId = "link-ui",
            Frequency = "Adhoc",
            ReportTypes = "NHSN",
            StartDate = "2026-10-01",
            DelayMinutes = "0",
            ReportTrackingId = "not-a-guid"
        }, numericOnly: false, Now, out _).Should().Contain("GUID");

        SystemRules.CheckReportScheduled(new ReportScheduledForm
        {
            FacilityId = "link-ui",
            Frequency = "Daily",
            ReportTypes = "NHSN, NHSN",
            StartDate = "2026-10-01",
            DelayMinutes = "15"
        }, numericOnly: false, Now, out var report).Should().BeNull();
        report!.ReportTypes.Should().Equal("NHSN");
        report.DelayMinutes.Should().Be(15);
        report.ReportTrackingId.Should().BeNull();

        SystemRules.CheckPatientList(new PatientListForm
        {
            FacilityId = "link-ui",
            ListType = "Transfer",
            TimeFrame = "LessThan24Hours",
            PatientIds = "p1"
        }, numericOnly: false, out _).Should().Contain("List type");

        SystemRules.CheckPatientList(new PatientListForm
        {
            FacilityId = "link-ui",
            ListType = "Admit",
            TimeFrame = "LessThan24Hours",
            PatientIds = " "
        }, numericOnly: false, out _).Should().Contain("patient");

        SystemRules.CheckPatientList(new PatientListForm
        {
            FacilityId = "link-ui",
            ListType = "Discharge",
            TimeFrame = "MoreThan48Hours",
            PatientIds = "p1 p1, p2"
        }, numericOnly: false, out var list).Should().BeNull();
        list!.PatientIds.Should().Equal("p1", "p2");
    }

    [Fact]
    public void Claims_stay_inside_the_catalog_and_an_empty_catalog_is_not_a_clear()
    {
        SystemRules.CheckClaims([], [], out _).Should().Contain("not changed");
        SystemRules.CheckClaims(["Other"], ["CanViewLogs"], out _).Should().Contain("Account service");
        SystemRules.CheckClaims(["CanViewLogs", "CanViewLogs"], ["CanViewLogs"], out var claims).Should().BeNull();
        claims.Should().Equal("CanViewLogs");
        SystemRules.CheckClaims([], ["CanViewLogs"], out var cleared).Should().BeNull();
        cleared.Should().BeEmpty();
    }

    [Fact]
    public void Patient_events_acquisition_requests_and_consumer_reads_are_checked()
    {
        SystemRules.CheckPatientEvent(new PatientEventForm
        {
            FacilityId = "link-ui",
            PatientId = "p1",
            EventType = "Transfer"
        }, numericOnly: false, out _).Should().Contain("Admission");

        SystemRules.CheckPatientEvent(new PatientEventForm
        {
            FacilityId = "link-ui",
            PatientId = "p1",
            EventType = "Discharge"
        }, numericOnly: false, out var patient).Should().BeNull();
        patient!.EventType.Should().Be("Discharge");

        SystemRules.CheckDataAcquisition(new DataAcquisitionForm
        {
            FacilityId = "link-ui",
            PatientId = "p1",
            QueryType = "Initial",
            ReportTypes = "NHSN",
            StartDate = "2026-10-06",
            EndDate = "2026-10-01"
        }, numericOnly: false, Now, out _).Should().Contain("before the end");

        SystemRules.CheckDataAcquisition(new DataAcquisitionForm
        {
            FacilityId = "link-ui",
            PatientId = "p1",
            QueryType = "Supplemental",
            ReportTypes = "NHSN, NHSN",
            StartDate = "2026-10-01",
            EndDate = "2026-10-06"
        }, numericOnly: false, Now, out var acquisition).Should().BeNull();
        acquisition!.ReportTypes.Should().Equal("NHSN");
        acquisition.QueryType.Should().Be("Supplemental");

        SystemRules.CheckPatientAcquired(new PatientAcquiredForm
        {
            FacilityId = "link-ui",
            PatientIds = "p1 p2"
        }, numericOnly: false, out var acquired).Should().BeNull();
        acquired!.PatientIds.Should().Equal("p1", "p2");

        SystemRules.CheckCorrelation(" ", generate: false, out _).Should().Contain("required");
        SystemRules.CheckCorrelation(" ", generate: true, out var generated).Should().BeNull();
        generated.Should().NotBe(Guid.Empty);

        var json = """{"ResourceNormalized":"[{\"patientId\":\"p1\",\"errorMessage\":\"missing\"}]"}""";
        SystemRules.ReadConsumers(json, out var topics).Should().BeNull();
        topics.Should().ContainSingle();
        topics[0].Topic.Should().Be("ResourceNormalized");
        topics[0].Events.Should().ContainSingle();
        topics[0].Events[0].PatientId.Should().Be("p1");
        topics[0].Events[0].Error.Should().Be("missing");

        SystemRules.ReadConsumers("""{"Other":"not-an-array"}""", out var raw).Should().BeNull();
        raw[0].Events[0].Error.Should().Be("not-an-array");
    }
}
