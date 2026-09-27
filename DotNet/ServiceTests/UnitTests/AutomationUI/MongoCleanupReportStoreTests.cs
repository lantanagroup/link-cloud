using System.Reflection;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class MongoCleanupReportStoreTests
{
    [Fact]
    public void Trim_drops_nothing_matched_before_passes_that_did_work()
    {
        var oldTeardown = Report(DateTimeOffset.Parse("2026-09-01T00:00:00Z"), tornDown: ["facility"]);
        var newerEmpty = Report(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));
        var newestEmpty = Report(DateTimeOffset.Parse("2026-09-22T00:00:00Z"));
        var failedEmpty = Report(DateTimeOffset.Parse("2026-09-21T00:00:00Z"), status: "failed");

        var drop = MongoCleanupReportStore.SelectIdsToTrim(
            [oldTeardown, newerEmpty, newestEmpty, failedEmpty],
            maxStored: 2);

        drop.Should().Equal(newerEmpty.Id, newestEmpty.Id);
    }

    [Fact]
    public void Document_stores_guids_as_strings_and_timestamps_as_bson_dates()
    {
        var documentType = typeof(MongoCleanupReportStore).GetNestedType("CleanupReportDocument", BindingFlags.NonPublic);
        documentType.Should().NotBeNull();
        Representation(documentType!, "Id").Should().Be(BsonType.String);
        Representation(documentType!, "StartedAt").Should().Be(BsonType.DateTime);
        Representation(documentType!, "FinishedAt").Should().Be(BsonType.DateTime);

        var document = Activator.CreateInstance(documentType!);
        var purged = Guid.NewGuid();
        var failed = Guid.NewGuid();
        documentType!.GetProperty("PurgedRunIds")!.SetValue(document, new List<Guid> { purged });
        documentType.GetProperty("FailedRunIds")!.SetValue(document, new List<Guid> { failed });

        var bson = document!.ToBsonDocument(documentType);
        bson["PurgedRunIds"].AsBsonArray.Single().BsonType.Should().Be(BsonType.String);
        bson["PurgedRunIds"].AsBsonArray.Single().AsString.Should().Be(purged.ToString());
        bson["FailedRunIds"].AsBsonArray.Single().BsonType.Should().Be(BsonType.String);
        bson["FailedRunIds"].AsBsonArray.Single().AsString.Should().Be(failed.ToString());
    }

    private static BsonType Representation(Type document, string property)
        => document.GetProperty(property)!
            .GetCustomAttribute<BsonRepresentationAttribute>()!
            .Representation;

    private static CleanupReport Report(DateTimeOffset finishedAt, string status = "completed", IReadOnlyList<string>? tornDown = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Status = status,
            FinishedAt = finishedAt,
            TornDownFacilityIds = tornDown ?? []
        };
}
