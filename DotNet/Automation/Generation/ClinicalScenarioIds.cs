namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Single source of truth for all clinical scenario GUIDs.
/// Every scenario in <see cref="FhirGenerationCodes.ClinicalScenarios"/> and any
/// eligibility/qualification logic must reference these constants rather than
/// duplicating GUID literals.
/// </summary>
/// <remarks>
/// Follow-up: expand the story catalog (more dx patterns) and add ACH-IP
/// non-qualifying stories (ambulatory, ED-only, observation) so a non-qualifying
/// ACH cohort has selectable scenarios. See <see cref="ClinicalScenarioEligibility"/>.
/// </remarks>
public static class ClinicalScenarioIds
{
    public static readonly Guid Pneumonia                = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a01");
    public static readonly Guid HeartFailure             = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a02");
    public static readonly Guid AcuteMyocardialInfarction = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a03");
    public static readonly Guid CopdExacerbation         = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a04");
    public static readonly Guid Sepsis                   = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a05");
    public static readonly Guid HipFracture              = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a06");
    public static readonly Guid AcuteRenalFailure        = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a07");
    public static readonly Guid IschemicStroke           = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a08");
    public static readonly Guid DiabeticKetoacidosis     = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a09");
    public static readonly Guid GastrointestinalBleed    = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a10");
    public static readonly Guid PulmonaryEmbolism        = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a11");
    public static readonly Guid AcutePancreatitis        = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a12");
    public static readonly Guid Cellulitis               = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a13");
    public static readonly Guid AtrialFibrillation       = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a14");
    public static readonly Guid DiabeticHypoglycemia     = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a15");
    public static readonly Guid Appendicitis             = new("f640c3ef-a8f4-4f85-87f1-1f1df74c8a16");
}
