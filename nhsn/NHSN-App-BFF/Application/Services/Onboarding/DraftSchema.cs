using System.Text.Json;
using System.Text.Json.Nodes;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;

// Versioning and migrate-on-read for OnboardingDrafts.DraftJson.
//
// Two independent deploy trains read this shape: the BFF, and a browser-cached nhsn-link.js that
// updates on its own schedule. Neither can assume the other has been upgraded, so both sides
// migrate — the UI has its own migrateDraft() in types.ts. This class covers the BFF's half: a row
// written by an older BFF, read by this one.
//
// The version lives in the SchemaVersion column, not inside the JSON. Migrate-on-read has to know
// which shape it's parsing before it parses it, and a version stored inside the document it
// describes can't be read without first assuming a shape.
public static class DraftSchema
{
    // Kept in lockstep with DRAFT_SCHEMA_VERSION in NHSN-App-UI/src/core/onboarding/types.ts. Bump
    // when a change is not readable by the previous shape, and add a case to Migrate. Adding an
    // optional field is not a bump — an older document simply lacks it and deserializes with the
    // default.
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // A draft is legitimately incomplete, so this is deliberately forgiving: unparseable or empty
    // JSON yields an empty state rather than throwing. Losing a step's UI flags is recoverable — the
    // user re-ticks a checkbox — whereas failing the read would lock them out of a facility whose
    // actual configuration is safe in Link and perfectly readable.
    public static OnboardingDraftState Read(string? draftJson, int storedVersion)
    {
        if (string.IsNullOrWhiteSpace(draftJson))
        {
            return new OnboardingDraftState();
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(draftJson);
        }
        catch (JsonException)
        {
            return new OnboardingDraftState();
        }

        if (node is null)
        {
            return new OnboardingDraftState();
        }

        node = Migrate(node, storedVersion);

        try
        {
            return node.Deserialize<OnboardingDraftState>(SerializerOptions) ?? new OnboardingDraftState();
        }
        catch (JsonException)
        {
            return new OnboardingDraftState();
        }
    }

    public static string Write(OnboardingDraftState state) =>
        JsonSerializer.Serialize(state, SerializerOptions);

    // Each case upgrades by exactly one version and falls through, so an old document passes
    // through every step on its way to current rather than needing a hand-written direct
    // transform. A version newer than this build is returned untouched — that happens on a
    // rollback, and since the older shape is a subset of the newer one, unknown fields are just
    // ignored on deserialization.
    private static JsonNode Migrate(JsonNode node, int storedVersion)
    {
        var version = storedVersion;

        while (version < CurrentVersion)
        {
            switch (version)
            {
                // case 1: MigrateV1ToV2(node); break;
                default:
                    // No transform registered. Deserialization tolerates missing fields, so the
                    // safe move is to stop rather than loop forever on an unknown version.
                    return node;
            }
        }

        return node;
    }
}
