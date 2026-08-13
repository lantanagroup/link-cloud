using FluentAssertions;
using LantanaGroup.Automation.Generation;
using System.Text.Json;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class OrgResourceMapPredictionFilterTests
{
    [Fact]
    public void Apply_honors_managingOrganization_fhirpath_condition()
    {
        const string orgCondition = "Location.managingOrganization.reference = 'Organization/ORG-1'";

        var entries = new List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>
        {
            Entry("Location", "L-ROOT", "Location/L-ROOT", """
                {
                  "resourceType":"Location",
                  "id":"L-ROOT",
                  "managingOrganization":{"reference":"Organization/ORG-1"}
                }
                """),
            Entry("Location", "L-ROOM", "Location/L-ROOM", """
                {
                  "resourceType":"Location",
                  "id":"L-ROOM",
                  "partOf":{"reference":"Location/L-ROOT"}
                }
                """),
            Entry("Encounter", "E-1", "Encounter/E-1", """
                {
                  "resourceType":"Encounter",
                  "id":"E-1",
                  "location":[{"location":{"reference":"Location/L-ROOM"}}]
                }
                """)
        };

        var acquired = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Encounter/E-1",
            "Location/L-ROOT",
            "Location/L-ROOM"
        };

        var filtered = OrgResourceMapPredictionFilter.Apply(
            acquired,
            entries,
            sharedResourceEntries: null,
            organizationLocationConditionFhirPaths: [orgCondition]);

        filtered.Should().Contain(["Encounter/E-1", "Location/L-ROOM"]);
        filtered.Should().NotContain("Location/L-ROOT");
    }

    [Fact]
    public void Apply_prunes_unreferenced_location_from_org_scoped_keys()
    {
        const string orgCondition = "Location.identifier.where(system='urn:test:loc' and value='org-root').exists()";

        var entries = new List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>
        {
            Entry("Location", "L-ROOT", "Location/L-ROOT", """
                {
                  "resourceType":"Location",
                  "id":"L-ROOT",
                  "identifier":[{"system":"urn:test:loc","value":"org-root"}]
                }
                """),
            Entry("Location", "L-ROOM", "Location/L-ROOM", """
                {
                  "resourceType":"Location",
                  "id":"L-ROOM",
                  "partOf":{"reference":"Location/L-ROOT"}
                }
                """),
            Entry("Location", "L-UNUSED", "Location/L-UNUSED", """
                {
                  "resourceType":"Location",
                  "id":"L-UNUSED",
                  "partOf":{"reference":"Location/L-ROOT"}
                }
                """),
            Entry("Encounter", "E-1", "Encounter/E-1", """
                {
                  "resourceType":"Encounter",
                  "id":"E-1",
                  "location":[{"location":{"reference":"Location/L-ROOM"}}]
                }
                """)
        };

        var acquired = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Encounter/E-1",
            "Location/L-ROOT",
            "Location/L-ROOM",
            "Location/L-UNUSED"
        };

        var filtered = OrgResourceMapPredictionFilter.Apply(
            acquired,
            entries,
            sharedResourceEntries: null,
            organizationLocationConditionFhirPaths: [orgCondition]);

        filtered.Should().Contain(["Encounter/E-1", "Location/L-ROOM"]);
        filtered.Should().NotContain(["Location/L-ROOT", "Location/L-UNUSED"]);
    }

    [Fact]
    public void Apply_does_not_keep_medication_referenced_only_by_cql_filtered_resource()
    {
        const string orgCondition = "Location.identifier.where(system='urn:test:loc' and value='org-root').exists()";

        var entries = new List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>
        {
            Entry("Location", "L-ROOT", "Location/L-ROOT", """
                {
                  "resourceType":"Location",
                  "id":"L-ROOT",
                  "identifier":[{"system":"urn:test:loc","value":"org-root"}]
                }
                """),
            Entry("Encounter", "E-1", "Encounter/E-1", """
                {
                  "resourceType":"Encounter",
                  "id":"E-1",
                  "location":[{"location":{"reference":"Location/L-ROOT"}}]
                }
                """),
            Entry("MedicationRequest", "MR-1", "MedicationRequest/MR-1", """
                {
                  "resourceType":"MedicationRequest",
                  "id":"MR-1",
                  "encounter":{"reference":"Encounter/E-1"},
                  "medicationReference":{"reference":"Medication/M-1"}
                }
                """),
            Entry("MedicationRequest", "MR-2", "MedicationRequest/MR-2", """
                {
                  "resourceType":"MedicationRequest",
                  "id":"MR-2",
                  "encounter":{"reference":"Encounter/E-1"},
                  "medicationReference":{"reference":"Medication/M-2"}
                }
                """),
            Entry("Medication", "M-1", "Medication/M-1", """
                {
                  "resourceType":"Medication",
                  "id":"M-1"
                }
                """),
            Entry("Medication", "M-2", "Medication/M-2", """
                {
                  "resourceType":"Medication",
                  "id":"M-2"
                }
                """)
        };

        var acquired = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Location/L-ROOT",
            "Encounter/E-1",
            "MedicationRequest/MR-1",
            "MedicationRequest/MR-2",
            "Medication/M-1",
            "Medication/M-2"
        };

        var cqlFiltered = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MedicationRequest/MR-2"
        };

        var filtered = OrgResourceMapPredictionFilter.Apply(
            acquired,
            entries,
            sharedResourceEntries: null,
            organizationLocationConditionFhirPaths: [orgCondition],
            cqlFilteredKeys: cqlFiltered);

        filtered.Should().Contain(["Medication/M-1", "MedicationRequest/MR-2"]);
        filtered.Should().NotContain("Medication/M-2");
    }

    private static (string ResourceType, string ResourceId, string Key, JsonElement Resource) Entry(
        string resourceType,
        string resourceId,
        string key,
        string json)
    {
        using var doc = JsonDocument.Parse(json);
        return (resourceType, resourceId, key, doc.RootElement.Clone());
    }
}
