using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;

// A single selectable checkbox option for one of the MRN Identifier Intake step's "select all that
// apply" questions (which MRN-like identifiers exist, how MRNs vary by facility, how they change
// over time). BFF-owned reference data, seeded by NhsnAppSeedData — the same for every facility,
// not captured from a user. LabelKey resolves through the existing /localization endpoint, same as
// every other UI string; this table only owns which values exist, in what order.
[Table("MrnIntakeOptionSets")]
public class MrnIntakeOptionSet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Which question this option belongs to: "MultipleMrnType", "MrnVarianceType", or
    // "MrnChangeType" — matches the union type names on the UI side.
    [MaxLength(64)]
    public string OptionGroup { get; set; } = string.Empty;

    // The value saved onto the facility's MrnIntake answers (e.g. "empi", "other").
    [MaxLength(64)]
    public string Value { get; set; } = string.Empty;

    [MaxLength(256)]
    public string LabelKey { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
