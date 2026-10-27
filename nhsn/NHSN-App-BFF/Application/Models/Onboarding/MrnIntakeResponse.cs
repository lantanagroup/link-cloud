namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

// Mirrors MrnIntake in NHSN-App-UI/src/core/api/contracts.ts. Normalized server-side (see
// MrnIntakeRecord) and read/written through its own GET/PUT /mrn-intake, never through
// PUT /onboarding — FacilityDraftResponse.MrnIntake below is a read-only mirror of this same
// shape, assembled onto the draft for rendering only.
public sealed record MrnIntakeResponse
{
    public bool HasMultipleMrn { get; init; }
    public IReadOnlyList<string> MultipleMrnTypes { get; init; } = [];
    public string? MultipleMrnOtherText { get; init; }
    public IReadOnlyList<string> UserFacingIdentifierNames { get; init; } = [];
    public bool IsCalledMrn { get; init; }
    public string? OtherTermUsed { get; init; }
    public bool CanSearchByIdentifier { get; init; }
    public bool VariesByFacility { get; init; }
    public IReadOnlyList<string> VarianceTypes { get; init; } = [];
    public string? VarianceOtherText { get; init; }
    public bool ChangesOverTime { get; init; }
    public IReadOnlyList<string> ChangeTypes { get; init; } = [];
    public string? ChangeOtherText { get; init; }
    public IReadOnlyList<MrnIdentifierRuleResponse> Rules { get; init; } = [];

    // Which element keys were added to Rules while a given patient's identifiers were on screen —
    // see rules.ts's MrnObservation on the UI side for how this backs the per-patient rule count
    // and the bidirectional add/remove between the rule list and the patient view.
    public IReadOnlyList<MrnObservationResponse> Observations { get; init; } = [];
}

public sealed record MrnIdentifierRuleResponse
{
    public int Ordinal { get; init; }
    public string Element { get; init; } = string.Empty;
    public string Rule { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;

    // Set only for a rule added from one patient's identifier card in the rule builder modal --
    // scopes the rule to that single identifier rather than every identifier with this element.
    // Null for a rule added through the plain "Rule to identify..." list, which stays general on
    // purpose. Mirrors MrnIdentifierRule.patientId/identifierIndex in contracts.ts.
    public string? PatientId { get; init; }
    public int? IdentifierIndex { get; init; }
}

public sealed record MrnObservationResponse
{
    public string PatientId { get; init; } = string.Empty;
    public IReadOnlyList<string> Elements { get; init; } = [];
}

// One patient's FHIR Patient.identifier array, for the rule builder's "Corresponding FHIR
// Identifier" table and detail view. Mirrors PatientIdentifier/PatientIdentifierElement in
// contracts.ts.
public sealed record PatientIdentifierResponse
{
    public string PatientId { get; init; } = string.Empty;
    public IReadOnlyList<PatientIdentifierElementResponse> Elements { get; init; } = [];
}

// Flattened fields, not a nested FHIR CodeableConcept/Period — Type is already the display text,
// not a coding, and PeriodStart/PeriodEnd are the two Period fields the rule builder can target.
public sealed record PatientIdentifierElementResponse
{
    public string Value { get; init; } = string.Empty;
    public string? Type { get; init; }
    public string? System { get; init; }
    public string? Use { get; init; }
    public string? Assigner { get; init; }
    public string? PeriodStart { get; init; }
    public string? PeriodEnd { get; init; }
}

// One selectable checkbox option for one of the step's "select all that apply" questions. Mirrors
// CheckboxOptionDef in the UI's mrn-intake/options.ts, minus the generic type parameter — the UI
// treats Value as an opaque string per question, same as MultipleMrnTypes/VarianceTypes/ChangeTypes
// above.
public sealed record MrnIntakeOptionResponse
{
    public string Value { get; init; } = string.Empty;
    public string LabelKey { get; init; } = string.Empty;
}

// GET /mrn-intake/options response — the three question option sets, in display order.
public sealed record MrnIntakeOptionsResponse
{
    public IReadOnlyList<MrnIntakeOptionResponse> MultipleMrnTypes { get; init; } = [];
    public IReadOnlyList<MrnIntakeOptionResponse> VarianceTypes { get; init; } = [];
    public IReadOnlyList<MrnIntakeOptionResponse> ChangeTypes { get; init; } = [];
}
