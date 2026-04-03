using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Automation.Generation.ResourceFactories;

public static class CensusListFactory
{
    /// <summary>Generate a census List resource for the given patient.</summary>
    public static List Generate(string id, string patientId, string listPrefix, DateTime date) =>
        Create(id, patientId, listPrefix, date);

    /// <summary>Create a census List with caller-supplied values.</summary>
    public static List Create(string id, string patientId, string listPrefix, DateTime date) => new()
    {
        Id = id,
        Status = List.ListStatus.Current,
        Mode = ListMode.Working,
        Title = $"Synthetic Census List - {listPrefix} - {patientId}",
        Code = new CodeableConcept
        {
            Coding = [new Coding("http://hl7.org/fhir/list-example-use-codes", "patients", "Patient List")],
            Text = "Patient List"
        },
        DateElement = new FhirDateTime(date),
        Entry =
        [
            new List.EntryComponent
            {
                Item = new ResourceReference($"Patient/{patientId}", $"Synthetic Patient {patientId}")
            }
        ]
    };
}
