namespace Automation.UI.Services;

/// <summary>
/// Well-known ids for seeded facility templates and the pieces they reference.
/// Epic and Cerner paths come from the ehr-test3 patient bundles named on LEGLINK-781
/// (Epic 019eb19d-249b-7ea8-8ddf-0e82340c1776, Cerner 019eb19f-a2a6-7de1-ae39-fa67552a7899).
/// </summary>
public static class FacilityTemplateCatalog
{
    public static readonly Guid SystemDefaultId = new("00000000-0000-0000-4000-000000000001");
    public static readonly Guid EpicId = new("00000000-0000-0000-4000-000000000002");
    public static readonly Guid CernerId = new("00000000-0000-0000-4000-000000000003");

    public static readonly Guid SystemQueryPlanId = new("00000000-0000-0000-1000-000000000001");
    public static readonly Guid CernerQueryPlanId = new("00000000-0000-0000-1000-000000000002");
    public static readonly Guid EpicQueryPlanId = new("00000000-0000-0000-1000-000000000003");

    public static readonly Guid SystemNormalizationSuiteId = new("00000000-0000-0000-2000-000000000100");
    public static readonly Guid CernerNormalizationSuiteId = new("00000000-0000-0000-2000-000000000101");
    public static readonly Guid EpicNormalizationSuiteId = new("00000000-0000-0000-2000-000000000102");
    public static readonly Guid CernerCleanupSequenceId = new("00000000-0000-0000-2000-000000000012");
    public static readonly Guid DefaultLocationSequenceId = new("00000000-0000-0000-2000-000000000010");
    public static readonly Guid DefaultCleanupSequenceId = new("00000000-0000-0000-2000-000000000011");
    public static readonly Guid CommonExtensionOperationId = new("00000000-0000-0000-2000-000000000002");

    public static readonly Guid SystemOrganizationResourceMapId = new("00000000-0000-0000-3000-000000000100");
    public static readonly Guid EpicOrganizationResourceMapId = new("00000000-0000-0000-3000-000000000101");
    public static readonly Guid CernerOrganizationResourceMapId = new("00000000-0000-0000-3000-000000000102");

    /// <summary>Epic location identifier system on every Location in the Epic bundle.</summary>
    public const string EpicLocationIdentifierSystem = "urn:oid:1.2.840.114350.1.13";

    /// <summary>Cerner facility codeset present on every Location.type in the Cerner bundle.</summary>
    public const string CernerLocationTypeSystem = "https://fhir.cerner.com/codeset/222";

    public const string RoleCodeSystem = "http://terminology.hl7.org/CodeSystem/v3-RoleCode";
    public const string HospitalRoleCode = "HOSP";

    /// <summary>
    /// Hospital buildings in the Epic bundle carry the Epic OID and RoleCode HOSP.
    /// Rooms and floors carry the OID without HOSP, so they stay children through partOf.
    /// </summary>
    public static string EpicOrgLocationFhirPath { get; } =
        $"identifier.where(system='{EpicLocationIdentifierSystem}').exists() and type.coding.where(system='{RoleCodeSystem}' and code='{HospitalRoleCode}').exists()";

    /// <summary>
    /// Every Cerner location carries codeset 222. Hospital buildings also carry RoleCode HOSP.
    /// Departments (CVDX, HRAD) and rooms do not, so they are not the org location.
    /// </summary>
    public static string CernerOrgLocationFhirPath { get; } =
        $"type.coding.where(system='{CernerLocationTypeSystem}').exists() and type.coding.where(system='{RoleCodeSystem}' and code='{HospitalRoleCode}').exists()";

    public static IReadOnlyList<Guid> SeededPatientConfigurationIds { get; } =
    [
        SystemPatientConfigurationIds.PneumoniaInpatient,
        SystemPatientConfigurationIds.DiabeticHypoglycemia,
        SystemPatientConfigurationIds.PneumoniaAmbulatory
    ];
}
