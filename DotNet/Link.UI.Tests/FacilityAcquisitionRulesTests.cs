using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class FacilityAcquisitionRulesTests
{
    [Fact]
    public void Fhir_query_without_auth_omits_authentication()
    {
        var built = FacilityAcquisitionRules.TryBuildFhirQuery(
            new FhirQueryPanel
            {
                FhirServerBaseUrl = "https://fhir.example.org/api/FHIR/R4",
                MaxConcurrentRequests = 4,
                MaxRetries = 2,
                MinPull = "06:00:00",
                MaxPull = "18:00:00"
            },
            "link-ui-p2b",
            "America/Chicago",
            out var body,
            out var error);

        built.Should().BeTrue(error);
        body!.ContainsKey("Authentication").Should().BeFalse();
        body["MinAcquisitionPullTime"].Should().Be("06:00:00");
        body["TimeZone"].Should().Be("America/Chicago");
        body.Should().NotContainKey("Id");
    }

    [Fact]
    public void Fhir_query_basic_auth_sends_secret_names()
    {
        var built = FacilityAcquisitionRules.TryBuildFhirQuery(
            new FhirQueryPanel
            {
                FhirServerBaseUrl = "https://fhir.example.org",
                MaxConcurrentRequests = 1,
                MaxRetries = 0,
                AuthEnabled = true,
                AuthType = "Basic",
                UserName = "kv-user",
                Password = "kv-password"
            },
            "link-ui-p2b",
            "America/Chicago",
            out var body,
            out var error);

        built.Should().BeTrue(error);
        var auth = body!["Authentication"].Should().BeAssignableTo<Dictionary<string, object?>>().Subject;
        auth["AuthType"].Should().Be("Basic");
        auth["UserName"].Should().Be("kv-user");
        auth["Password"].Should().Be("kv-password");
    }

    [Fact]
    public void Overnight_pull_window_validates_and_is_sent()
    {
        var built = FacilityAcquisitionRules.TryBuildFhirQuery(
            new FhirQueryPanel
            {
                FhirServerBaseUrl = "https://fhir.example.org",
                MaxConcurrentRequests = 1,
                MaxRetries = 0,
                MinPull = "18:00:00",
                MaxPull = "06:00:00"
            },
            "link-ui-p2b",
            "America/Chicago",
            out var body,
            out var error);

        built.Should().BeTrue(error);
        body!["MinAcquisitionPullTime"].Should().Be("18:00:00");
        body["MaxAcquisitionPullTime"].Should().Be("06:00:00");
        FacilityAcquisitionRules.PullWindowHint("18:00:00", "06:00:00")
            .Should().Be(FacilityAcquisitionRules.OvernightWindowHint);
    }

    [Fact]
    public void Equal_pull_times_stay_valid_and_show_no_overnight_hint()
    {
        var built = FacilityAcquisitionRules.TryBuildFhirQuery(
            new FhirQueryPanel
            {
                FhirServerBaseUrl = "https://fhir.example.org",
                MaxConcurrentRequests = 1,
                MaxRetries = 0,
                MinPull = "18:00:00",
                MaxPull = "18:00:00"
            },
            "link-ui-p2b",
            "America/Chicago",
            out var body,
            out var error);

        built.Should().BeTrue(error);
        body!["MinAcquisitionPullTime"].Should().Be("18:00:00");
        body["MaxAcquisitionPullTime"].Should().Be("18:00:00");
        FacilityAcquisitionRules.PullWindowHint("18:00:00", "18:00:00").Should().BeNull();
        FacilityAcquisitionRules.PullWindowHint("06:00:00", "18:00:00").Should().BeNull();
        FacilityAcquisitionRules.PullWindowHint(null, null).Should().BeNull();
    }

    [Fact]
    public void Blank_secret_names_keep_the_saved_value()
    {
        var saved = new FhirQueryPanel
        {
            UserName = "kv-user",
            Password = "kv-password",
            AuthKey = "kv-key",
            ClientId = "kv-client",
            ClientSecret = "kv-secret",
            CustomHeaders = new List<HeaderInput> { new() { Key = "X-Api", Value = "kv-header" } }
        };
        var posted = new FhirQueryPanel
        {
            UserName = " ",
            Password = "",
            AuthKey = "",
            ClientId = "",
            ClientSecret = "replaced",
            CustomHeaders = new List<HeaderInput>
            {
                new() { Key = "X-Api", Value = "" },
                new() { Key = "X-New", Value = "" }
            }
        };

        FacilityAcquisitionRules.KeepBlankSecrets(saved, posted);

        posted.UserName.Should().Be("kv-user");
        posted.Password.Should().Be("kv-password");
        posted.AuthKey.Should().Be("kv-key");
        posted.ClientId.Should().Be("kv-client");
        posted.ClientSecret.Should().Be("replaced");
        posted.CustomHeaders[0].Value.Should().Be("kv-header");
        posted.CustomHeaders[1].Value.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Fhir_query_rejects_one_sided_pull_times()
    {
        var built = FacilityAcquisitionRules.TryBuildFhirQuery(
            new FhirQueryPanel
            {
                FhirServerBaseUrl = "https://fhir.example.org",
                MaxConcurrentRequests = 1,
                MaxRetries = 0,
                MinPull = "06:00:00"
            },
            "link-ui-p2b",
            "America/Chicago",
            out _,
            out var error);

        built.Should().BeFalse();
        error.Should().Contain("both");
    }

    [Fact]
    public void Fhir_query_round_trips_case_insensitive_json()
    {
        var panel = FacilityAcquisitionRules.ParseFhirQuery(
            """
            {
              "id": "cfg-1",
              "fhirServerBaseUrl": "https://fhir.example.org",
              "maxConcurrentRequests": 8,
              "maxRetries": 1,
              "minAcquisitionPullTime": "06:00:00",
              "authentication": {
                "authType": "Epic",
                "key": "kv-key",
                "tokenUrl": "https://auth.example.org/token",
                "audience": "fhir",
                "clientId": "kv-client"
              }
            }
            """);

        panel.Exists.Should().BeTrue();
        panel.Id.Should().Be("cfg-1");
        panel.MaxConcurrentRequests.Should().Be(8);
        panel.MinPull.Should().Be("06:00:00");
        panel.AuthEnabled.Should().BeTrue();
        panel.AuthType.Should().Be("Epic");
        panel.AuthKey.Should().Be("kv-key");
        panel.CustomHeaders.Should().ContainSingle();
    }

    [Fact]
    public void Fhir_list_requires_six_unique_ids()
    {
        var panel = FacilityAcquisitionRules.EmptyFhirList();
        panel.FhirBaseServerUrl = "https://fhir.example.org";
        for (var i = 0; i < panel.Lists.Count; i++)
            panel.Lists[i].FhirId = $"list-{i}";

        var built = FacilityAcquisitionRules.TryBuildFhirList(panel, "link-ui-p2b", "kept-auth", out var body, out var error);

        built.Should().BeTrue(error);
        body!["Authentication"].Should().Be("kept-auth");
        var rows = body["EHRPatientLists"].Should().BeAssignableTo<List<Dictionary<string, object?>>>().Subject;
        rows.Should().HaveCount(6);
        rows[0]["Status"].Should().Be("Admit");
        rows[0]["TimeFrame"].Should().Be("LessThan24Hours");

        panel.Lists[1].FhirId = "list-0";
        FacilityAcquisitionRules.TryBuildFhirList(panel, "link-ui-p2b", null, out _, out error).Should().BeFalse();
        error.Should().Contain("more than one");

        panel.Lists[1].FhirId = "";
        FacilityAcquisitionRules.TryBuildFhirList(panel, "link-ui-p2b", null, out _, out error).Should().BeFalse();
        error.Should().Contain("required");
    }

    [Fact]
    public void Query_plan_uses_string_discriminators_and_skips_blank_rows()
    {
        var panel = new QueryPlanPanel
        {
            Type = "daily",
            PlanName = "Daily census",
            EhrDescription = "Epic lists",
            LookBack = "P30D",
            InitialQueries =
            [
                new QueryRowInput
                {
                    ResourceType = "Patient",
                    QueryConfigType = "Parameter",
                    OperationType = "Search",
                    Parameters =
                    [
                        new QueryParameterInput { Name = "id", ParameterType = "Variable", Variable = "PatientId" },
                        new QueryParameterInput()
                    ]
                },
                new QueryRowInput
                {
                    ResourceType = "Encounter",
                    QueryConfigType = "Reference",
                    OperationType = "Read",
                    Paged = 25
                },
                new QueryRowInput()
            ],
            SupplementalQueries =
            [
                new QueryRowInput
                {
                    ResourceType = "Observation",
                    QueryConfigType = "Parameter",
                    OperationType = "SearchPost",
                    Parameters = [new QueryParameterInput { Name = "patient", ParameterType = "Literal", Literal = "example" }]
                }
            ]
        };

        var built = FacilityAcquisitionRules.TryBuildQueryPlan(panel, "link-ui-p2b", out var body, out var error);

        built.Should().BeTrue(error);
        body!["Type"].Should().Be("Daily");
        var initial = body["InitialQueries"].Should().BeAssignableTo<Dictionary<string, object?>>().Subject;
        initial.Keys.Should().Equal("0", "1");
        var parameterQuery = initial["0"].Should().BeAssignableTo<Dictionary<string, object?>>().Subject;
        parameterQuery["QueryConfigType"].Should().Be("Parameter");
        parameterQuery.Should().NotContainKey("Paged");
        var parameters = parameterQuery["Parameters"].Should().BeAssignableTo<List<Dictionary<string, object?>>>().Subject;
        parameters.Should().ContainSingle();
        parameters[0]["ParameterType"].Should().Be("Variable");
        var referenceQuery = initial["1"].Should().BeAssignableTo<Dictionary<string, object?>>().Subject;
        referenceQuery["QueryConfigType"].Should().Be("Reference");
        referenceQuery["Paged"].Should().Be(25);
        referenceQuery.Should().NotContainKey("Parameters");
    }

    [Fact]
    public void Query_plan_rejects_a_repeated_initial_resource()
    {
        var panel = new QueryPlanPanel
        {
            Type = "Discharge",
            PlanName = "Discharge",
            EhrDescription = "Epic",
            LookBack = "PT2H",
            InitialQueries =
            [
                Query("Patient"),
                Query("Patient")
            ],
            SupplementalQueries = [Query("Observation")]
        };

        var built = FacilityAcquisitionRules.TryBuildQueryPlan(panel, "link-ui-p2b", out _, out var error);

        built.Should().BeFalse();
        error.Should().Contain("Patient");
    }

    [Fact]
    public void Query_plan_parses_string_discriminators()
    {
        var panel = FacilityAcquisitionRules.ParseQueryPlan(
            """
            {
              "planName": "Daily",
              "eHRDescription": "Epic lists",
              "lookBack": "P7D",
              "type": "Daily",
              "initialQueries": {
                "0": {
                  "queryConfigType": "Parameter",
                  "resourceType": "Patient",
                  "operationType": "Search",
                  "parameters": [{ "parameterType": "Literal", "name": "_id", "literal": "1" }]
                }
              },
              "supplementalQueries": {}
            }
            """,
            "Daily");

        panel.EhrDescription.Should().Be("Epic lists");
        panel.InitialQueries[0].QueryConfigType.Should().Be("Parameter");
        panel.InitialQueries[0].Parameters[0].Literal.Should().Be("1");
        panel.InitialQueries.Should().HaveCount(2);
    }

    [Fact]
    public void Reporting_organization_builds_and_parses_identifier_paths()
    {
        var built = FacilityAcquisitionRules.TryBuildReportingOrg(
            new ReportingOrgPanel
            {
                Description = "Ward",
                IsActive = true,
                SetupMethod = "identifier",
                Matches =
                [
                    new ReportingMatchInput { IdentifierSystem = "http://example.org", IdentifierCode = "A" },
                    new ReportingMatchInput { IdentifierSystem = "http://example.org", IdentifierCode = "O'Brien" },
                    new ReportingMatchInput()
                ]
            },
            out var body,
            out var error);

        built.Should().BeTrue(error);
        body!.Conditions.Should().ContainSingle();
        body.Conditions[0].Priority.Should().Be(1);
        body.Conditions[0].FhirPath.Should().Contain(" or ");

        var parsed = FacilityAcquisitionRules.ParseReportingOrg(
            [
                new OrganizationLocationConfigurationApiModel
                {
                    ConfigId = 7,
                    Description = "Ward",
                    IsActive = true,
                    Conditions = [new OrganizationLocationConditionApiModel { FhirPath = body.Conditions[0].FhirPath, Priority = 1 }]
                }
            ],
            selectedId: null);

        parsed.ConfigId.Should().Be(7);
        parsed.SetupMethod.Should().Be("identifier");
        parsed.Matches.Should().HaveCount(3);
        parsed.Matches[1].IdentifierCode.Should().Be("O'Brien");
    }

    [Fact]
    public void Reporting_organization_keeps_an_unparsed_path_manual()
    {
        var parsed = FacilityAcquisitionRules.ParseReportingOrg(
            [
                new OrganizationLocationConfigurationApiModel
                {
                    ConfigId = 3,
                    IsActive = false,
                    Conditions = [new OrganizationLocationConditionApiModel { FhirPath = "Location.name = 'Other'" }]
                }
            ],
            3);

        parsed.SetupMethod.Should().Be("manual");
        parsed.FhirPath.Should().Be("Location.name = 'Other'");
    }

    [Fact]
    public void Sftp_body_never_contains_the_password()
    {
        var built = FacilityAcquisitionRules.TryBuildSftp(
            new SftpPanel
            {
                Host = "sftp.example.org",
                Port = 22,
                RemoteDirectory = "/inbox",
                Timeout = "00:01:00",
                Username = "pickup",
                Password = "not-stored-in-config",
                Acquisitions =
                [
                    new SftpAcquisitionInput
                    {
                        AcquisitionType = "Census",
                        SubType = "None",
                        FileNamePattern = "*.csv"
                    },
                    new SftpAcquisitionInput()
                ]
            },
            "link-ui-p2b",
            out var body,
            out var credentials,
            out var error);

        built.Should().BeTrue(error);
        body!.ContainsKey("Password").Should().BeFalse();
        body.ContainsKey("Username").Should().BeFalse();
        body["AuthenticationProtocol"].Should().Be("Basic");
        body["Timeout"].Should().Be("00:01:00");
        credentials.Should().NotBeNull();
        credentials!.Value.Username.Should().Be("pickup");
        credentials.Value.Password.Should().Be("not-stored-in-config");
        var acquisitions = body["AcquisitionConfigurations"].Should().BeAssignableTo<List<Dictionary<string, object?>>>().Subject;
        acquisitions.Should().ContainSingle();
        acquisitions[0].Should().NotContainKey("RemoteDirectory");
    }

    [Fact]
    public void Sftp_rejects_a_password_without_a_username()
    {
        var built = FacilityAcquisitionRules.TryBuildSftp(
            new SftpPanel
            {
                Host = "sftp.example.org",
                Port = 22,
                RemoteDirectory = "/",
                Timeout = "00:01:00",
                Password = "only-password"
            },
            "link-ui-p2b",
            out _,
            out var credentials,
            out var error);

        built.Should().BeFalse();
        credentials.Should().BeNull();
        error.Should().Contain("together");
    }

    [Fact]
    public void Sftp_parse_reads_acquisition_rows_and_credential_status()
    {
        var panel = FacilityAcquisitionRules.ParseSftp(
            """
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "host": "sftp.example.org",
              "port": 2222,
              "remoteDirectory": "/inbox",
              "timeout": "00:02:00",
              "removeAfterProcessing": true,
              "acquisitionConfigurations": [
                { "acquisitionType": "Resources", "subType": "CernerCCLExtract", "fileNamePattern": "*.txt" }
              ]
            }
            """);

        panel.ConfigurationId.Should().Be("11111111-1111-1111-1111-111111111111");
        panel.Port.Should().Be(2222);
        panel.Timeout.Should().Be("00:02:00");
        panel.RemoveAfterProcessing.Should().BeTrue();
        panel.Acquisitions[0].AcquisitionType.Should().Be("Resources");
        panel.Acquisitions.Should().HaveCount(2);
        FacilityAcquisitionRules.ParseCredentialStatus("""{"hasCredentials":true}""").Should().BeTrue();
        FacilityAcquisitionRules.ParseCredentialStatus("""{"hasCredentials":false}""").Should().BeFalse();
    }

    private static QueryRowInput Query(string resource) => new()
    {
        ResourceType = resource,
        QueryConfigType = "Parameter",
        OperationType = "Search",
        Parameters = [new QueryParameterInput { Name = "id", ParameterType = "Variable", Variable = "PatientId" }]
    };
}
