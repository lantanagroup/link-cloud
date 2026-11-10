using Automation.UI.Models.ApiHealth;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.DataAcquisitionSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

public sealed partial class DataAcquisitionTestSuite
{
    private const string InvalidGuidProbe = "not-a-guid";
    private const string InvalidFhirServerUrl = "not-a-url";
    private const string SyntheticPatientId = "ApiHealth-patient";
    private const string SyntheticEncounterId = "ApiHealth-encounter";

    private async Task AppendOrganizationLocationCoverageAsync(CoverageState state)
    {
        var facilityId = state.FacilityId;
        var ct = state.CancellationToken;
        var inactive = new { Description = "ApiHealth throwaway organization location configuration", IsActive = false };

        if (state.OrgLocationConfigId > 0)
        {
            await AddStepAsync(state, StepNames.OrgLocationConfigGet200ById, 200, async () =>
                await _client.GetOrganizationLocationConfigurationByIdAsync(state.OrgLocationConfigId, ct));
        }
        else
        {
            state.Results.Add(SkipStepAsync(
                StepNames.OrgLocationConfigGet200ById,
                "Organization location configuration create did not return a config id."));
        }

        await AddStepAsync(state, StepNames.OrgLocationConfigGet404, 404, async () =>
            await _client.GetOrganizationLocationConfigurationByIdAsync(int.MaxValue, ct));

        await AddStepAsync(state, StepNames.OrgLocationConfigSearch200, 200, () =>
            _client.SearchOrganizationLocationConfigurationsAsync(facilityId, ct));

        await AddStepAsync(state, StepNames.OrgLocationConfigPut404, 404, () =>
            _client.UpdateOrganizationLocationConfigurationByIdAsync(int.MaxValue, inactive, ct));

        if (state.OrgLocationConfigId > 0)
        {
            await AddStepAsync(state, StepNames.OrgLocationConfigPut202ById, 202, () =>
                _client.UpdateOrganizationLocationConfigurationByIdAsync(state.OrgLocationConfigId, inactive, ct));
        }
        else
        {
            state.Results.Add(SkipStepAsync(
                StepNames.OrgLocationConfigPut202ById,
                "Organization location configuration create did not return a config id."));
        }

        if (state.OrgLocationConfigCreated)
        {
            await AddStepAsync(state, StepNames.OrgLocationConfigPut202, 202, () =>
                _client.UpdateOrganizationLocationConfigurationAsync(facilityId, inactive, ct));
        }
        else
        {
            state.Results.Add(SkipStepAsync(
                StepNames.OrgLocationConfigPut202,
                "Organization location configuration was not created, so the facility update was not sent."));
        }

        await AddStepAsync(state, StepNames.OrgLocationMappingGet404, 404, async () =>
            await _client.GetOrganizationLocationMappingAsync(int.MaxValue, ct));

        await AddStepAsync(state, StepNames.OrgLocationMappingSearch200, 200, () =>
            _client.SearchOrganizationLocationMappingsAsync(facilityId, ct));

        await AddStepAsync(state, StepNames.OrgLocationMappingPut404, 404, () =>
            _client.UpdateOrganizationLocationMappingAsync(int.MaxValue, new { IsOrgLocation = false }, ct));

        await AddStepAsync(state, StepNames.OrgLocationMappingDelete202, 202, () =>
            _client.DeleteOrganizationLocationMappingAsync(int.MaxValue, ct));

        state.Results.Add(await RunStepAsync(StepNames.OrgLocationMappingsDelete202, 202, async () =>
        {
            var response = await _client.DeleteOrganizationLocationMappingsByFacilityAsync(facilityId, ct);
            if (response.IsSuccessStatusCode)
                state.OrgLocationMappingsDeleted = true;
            return response;
        }, ct));

        if (state.OrgLocationConfigId > 0)
        {
            state.Results.Add(await RunStepAsync(StepNames.OrgLocationConfigDelete202ById, 202, async () =>
            {
                var response = await _client.DeleteOrganizationLocationConfigurationByIdAsync(state.OrgLocationConfigId, ct);
                if (response.IsSuccessStatusCode)
                    state.OrgLocationConfigDeleted = true;
                return response;
            }, ct));
        }
        else
        {
            state.Results.Add(SkipStepAsync(
                StepNames.OrgLocationConfigDelete202ById,
                "Organization location configuration create did not return a config id."));
        }

        state.Results.Add(await RunStepAsync(StepNames.OrgLocationConfigDelete202, 202, async () =>
        {
            var response = await _client.DeleteOrganizationLocationConfigurationsAsync(facilityId, ct);
            if (response.IsSuccessStatusCode)
                state.OrgLocationConfigDeleted = true;
            return response;
        }, ct));
    }

    private async Task AppendEncounterCoverageAsync(CoverageState state)
    {
        var facilityId = state.FacilityId;
        var ct = state.CancellationToken;
        var mapping = new { MappedToOrg = false };

        await AddStepAsync(state, StepNames.EncounterMappingGet404, 404, () =>
            _client.GetEncounterMappingByIdAsync(int.MaxValue, ct));

        await AddStepAsync(state, StepNames.EncounterMappingByEncounterGet404, 404, () =>
            _client.GetEncounterMappingByEncounterAsync(facilityId, SyntheticEncounterId, ct));

        await AddStepAsync(state, StepNames.EncounterMappingsByPatientGet200, 200, () =>
            _client.GetEncounterMappingsByPatientAsync(facilityId, SyntheticPatientId, ct));

        await AddStepAsync(state, StepNames.EncounterMappingSearch200, 200, () =>
            _client.SearchEncounterMappingsAsync(facilityId, ct));

        await AddStepAsync(state, StepNames.EncounterMappingPut404, 404, () =>
            _client.UpdateEncounterMappingAsync(int.MaxValue, mapping, ct));

        await AddStepAsync(state, StepNames.EncounterMappingPutByEncounter404, 404, () =>
            _client.UpdateEncounterMappingByEncounterAsync(facilityId, SyntheticEncounterId, mapping, ct));

        await AddStepAsync(state, StepNames.EncounterMappingPost400, 400, () =>
            _client.CreateEncounterMappingAsync(new { }, ct));
    }

    private async Task AppendFhirConfigurationCoverageAsync(CoverageState state)
    {
        var facilityId = state.FacilityId;
        var ct = state.CancellationToken;

        await AddStepAsync(state, StepNames.FhirConfigPut202, 202, () =>
            _client.UpdateFhirQueryConfigurationAsync(BuildFhirConfigRequest(facilityId), ct));

        await AddStepAsync(state, StepNames.AuthGet404, 404, () =>
            _client.GetAuthenticationConfigurationAsync(facilityId, ct));

        await AddStepAsync(state, StepNames.AuthPost400, 400, () =>
            _client.CreateAuthenticationConfigurationAsync(facilityId, new { }, ct));

        await AddStepAsync(state, StepNames.AuthPut400, 400, () =>
            _client.UpdateAuthenticationConfigurationAsync(facilityId, new { }, ct));

        await AddStepAsync(state, StepNames.AuthDelete202, 202, () =>
            _client.DeleteAuthenticationConfigurationAsync(facilityId, ct));
    }

    private Task AppendQueryPlanUpdateAsync(CoverageState state) =>
        AddStepAsync(state, StepNames.QueryPlanPut202, 202, () =>
            _client.UpdateQueryPlanAsync(state.FacilityId, BuildDischargePlanRequest(state.FacilityId, "ApiHealth-Discharge"), state.CancellationToken));

    private Task AppendQueryPlanDeleteAllAsync(CoverageState state) =>
        AddStepAsync(state, StepNames.QueryPlanDeleteAll202, 202, () =>
            _client.DeleteAllQueryPlansAsync(state.FacilityId, state.CancellationToken));

    private Task AppendFhirListUpdateAsync(CoverageState state) =>
        AddStepAsync(state, StepNames.FhirListConfigPut200, 200, () =>
            _client.UpdateFhirListConfigurationAsync(BuildFhirListConfigRequest(state.FacilityId), state.CancellationToken));

    private async Task AppendLogAndConnectionCoverageAsync(CoverageState state, string reportId)
    {
        var facilityId = state.FacilityId;
        var ct = state.CancellationToken;

        await AddStepAsync(state, StepNames.StatisticsGet200, 200, () =>
            _client.GetReportStatisticsAsync(reportId, ct));

        await AddStepAsync(state, StepNames.ResourceCountsGet200, 200, async () =>
            await _client.GetAcquiredResourceCountsByPatientAsync(facilityId, reportId, ct));

        await AddStepAsync(state, StepNames.ResourceCountsGet400, 400, async () =>
            await _client.GetAcquiredResourceCountsByPatientAsync(facilityId, InvalidGuidProbe, ct));

        await AddStepAsync(state, StepNames.LogsByFacilityGet200, 200, () =>
            _client.SearchAcquisitionLogsByFacilityAsync(facilityId, ct));

        await AddStepAsync(state, StepNames.LogsByPatientGet200, 200, () =>
            _client.SearchAcquisitionLogsByPatientAsync(facilityId, SyntheticPatientId, ct));

        await AddStepAsync(state, StepNames.UpdateLog400, 400, () =>
            _client.UpdateAcquisitionLogAsync(0, new { }, ct));

        await AddStepAsync(state, StepNames.FacilityValidateGet400, 400, () =>
            _client.ValidateFacilityConnectionAsync(facilityId, cancellationToken: ct));

        await AddStepAsync(state, StepNames.FhirServerValidateGet400, 400, async () =>
            await _client.ValidateFhirServerConnectionAsync(InvalidFhirServerUrl, ct));

        await AddStepAsync(state, StepNames.FhirAuthGet404, 404, () =>
            _client.GetFhirAuthenticationConfigurationAsync(facilityId, ct));

        await AddStepAsync(state, StepNames.FhirAuthPut400, 400, () =>
            _client.UpdateFhirAuthenticationConfigurationAsync(facilityId, new { }, ct));

        await AddStepAsync(state, StepNames.FhirQueriesGet404, 404, () =>
            _client.GetFhirQueriesAsync(facilityId, ct));
    }

    private async Task AppendSftpCoverageAsync(CoverageState state)
    {
        var facilityId = state.FacilityId;
        var ct = state.CancellationToken;

        await AddStepAsync(state, StepNames.SftpConfigGet400, 400, () =>
            _client.GetSftpConfigurationByIdAsync(Guid.Empty, ct));

        await AddStepAsync(state, StepNames.SftpConfigByOrgGet404, 404, () =>
            _client.GetOrganizationSftpConfigurationAsync(facilityId, ct));

        await AddStepAsync(state, StepNames.SftpConfigPost400, 400, () =>
            _client.CreateSftpConfigurationAsync(facilityId, new { }, ct));

        await AddStepAsync(state, StepNames.SftpConfigPut400, 400, () =>
            _client.UpdateSftpConfigurationAsync(facilityId, Guid.NewGuid().ToString("D"), new { }, ct));

        await AddStepAsync(state, StepNames.SftpConfigDelete400, 400, () =>
            _client.DeleteSftpConfigurationAsync(facilityId, InvalidGuidProbe, ct));

        await AddStepAsync(state, StepNames.SftpCredentialsPut400, 400, () =>
            _client.UpdateSftpCredentialsAsync(facilityId, new { }, ct));

        await AddStepAsync(state, StepNames.SftpCredentialStatusGet404, 404, () =>
            _client.GetSftpCredentialStatusAsync(facilityId, ct));

        await AddStepAsync(state, StepNames.SftpCredentialsDelete404, 404, () =>
            _client.DeleteSftpCredentialsAsync(facilityId, ct));

        await AddStepAsync(state, StepNames.SftpSavedTestPost404, 404, () =>
            _client.TestSavedSftpConnectionAsync(facilityId, ct));

        await AddStepAsync(state, StepNames.SftpAdHocTestPost400, 400, async () =>
            await _client.TestSftpConnectionAsync(new SftpTestConnectionRequestApiModel(), cancellationToken: ct));

        await AddStepAsync(state, StepNames.SftpLogsGet200, 200, () =>
            _client.SearchSftpLogsAsync(cancellationToken: ct));

        await AddStepAsync(state, StepNames.SftpLogGet400, 400, () =>
            _client.GetSftpLogAsync(InvalidGuidProbe, ct));

        await AddStepAsync(state, StepNames.SftpLogPost400, 400, () =>
            _client.CreateSftpLogAsync(new { }, ct));

        await AddStepAsync(state, StepNames.SftpLogReset400, 400, () =>
            _client.ResetSftpLogAsync(InvalidGuidProbe, ct));
    }

    private async Task AddStepAsync(
        CoverageState state,
        string name,
        int expectedStatusCode,
        Func<Task<LinkApiResponse>> action)
    {
        state.Results.Add(await RunStepAsync(name, expectedStatusCode, action, state.CancellationToken));
    }

    private sealed class CoverageState
    {
        public CoverageState(string facilityId, List<ApiTestRunResult> results, CancellationToken cancellationToken)
        {
            FacilityId = facilityId;
            Results = results;
            CancellationToken = cancellationToken;
        }

        public string FacilityId { get; }
        public List<ApiTestRunResult> Results { get; }
        public CancellationToken CancellationToken { get; }
        public int OrgLocationConfigId { get; set; }
        public bool OrgLocationConfigCreated { get; set; }
        public bool OrgLocationConfigDeleted { get; set; }
        public bool OrgLocationMappingsDeleted { get; set; }
    }
}
