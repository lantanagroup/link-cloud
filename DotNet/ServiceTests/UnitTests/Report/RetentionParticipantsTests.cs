using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace UnitTests.Report;

/// <summary>
/// Covers how <see cref="RetentionParticipants"/> reads and validates Retention:Participants.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionParticipantsTests
{
    [Fact]
    public void FromConfiguration_KeyNotSet_ReturnsFourReportScopedDefaults()
    {
        var participants = Read(new Dictionary<string, string?>());

        Assert.True(participants.IsValid);
        Assert.True(participants.IsDefault);
        Assert.Equal(["Report", "MeasureEval", "Validation", "DataAcquisition"],
                     participants.Participants.Select(p => p.ServiceName));
        Assert.All(participants.Participants, p => Assert.Equal(RetentionParticipantScope.Report, p.Scope));
    }

    [Fact]
    public void FromConfiguration_ValidList_ReturnsItInIndexOrder()
    {
        var participants = Read(new Dictionary<string, string?>
        {
            ["Retention:Participants:10:ServiceName"] = "Audit",
            ["Retention:Participants:10:Scope"] = "Cutoff",
            ["Retention:Participants:2:ServiceName"] = " Report ",
            ["Retention:Participants:2:Scope"] = "report"
        });

        Assert.True(participants.IsValid);
        Assert.False(participants.IsDefault);
        Assert.Equal([new RetentionParticipant("Report", RetentionParticipantScope.Report),
                      new RetentionParticipant("Audit", RetentionParticipantScope.Cutoff)],
                     participants.Participants);
    }

    [Fact]
    public void FromConfiguration_EmptyJsonArray_IsInvalid()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream("""{ "Retention": { "Participants": [] } }"""u8.ToArray()))
            .Build();

        var participants = RetentionParticipants.FromConfiguration(configuration);

        Assert.False(participants.IsValid);
        Assert.Empty(participants.Participants);
        Assert.Contains("lists no participants", Assert.Single(participants.Errors));
    }

    [Fact]
    public void FromConfiguration_EmptyValue_IsInvalid()
    {
        var participants = Read(new Dictionary<string, string?>
        {
            ["Retention:Participants"] = ""
        });

        Assert.False(participants.IsValid);
        Assert.Contains("lists no participants", Assert.Single(participants.Errors));
    }

    [Fact]
    public void FromConfiguration_OtherRetentionKeysOnly_ReturnsDefaults()
    {
        var participants = Read(new Dictionary<string, string?>
        {
            ["Retention:Schedule:Cron"] = "0 0 3 * * ?"
        });

        Assert.True(participants.IsDefault);
    }

    [Fact]
    public void FromConfiguration_MissingServiceName_IsInvalid()
    {
        var participants = Read(new Dictionary<string, string?>
        {
            ["Retention:Participants:0:ServiceName"] = "Report",
            ["Retention:Participants:0:Scope"] = "Report",
            ["Retention:Participants:1:ServiceName"] = "  ",
            ["Retention:Participants:1:Scope"] = "Report"
        });

        Assert.False(participants.IsValid);
        Assert.Empty(participants.Participants);
        Assert.Equal("Retention:Participants:1 has no ServiceName.", Assert.Single(participants.Errors));
    }

    [Fact]
    public void FromConfiguration_MissingScope_IsInvalid()
    {
        var participants = Read(new Dictionary<string, string?>
        {
            ["Retention:Participants:0:ServiceName"] = "Report"
        });

        Assert.False(participants.IsValid);
        Assert.Equal("Retention:Participants:0 has no Scope.", Assert.Single(participants.Errors));
    }

    [Theory]
    [InlineData("Facility")]
    [InlineData("1")]
    public void FromConfiguration_UnknownScope_IsInvalid(string scope)
    {
        var participants = Read(new Dictionary<string, string?>
        {
            ["Retention:Participants:0:ServiceName"] = "Report",
            ["Retention:Participants:0:Scope"] = scope
        });

        Assert.False(participants.IsValid);
        Assert.Equal($"Retention:Participants:0:Scope '{scope}' is not one of Report, Cutoff.",
                     Assert.Single(participants.Errors));
    }

    [Fact]
    public void FromConfiguration_DuplicateNameDifferingInCase_IsInvalid()
    {
        var participants = Read(new Dictionary<string, string?>
        {
            ["Retention:Participants:0:ServiceName"] = "MeasureEval",
            ["Retention:Participants:0:Scope"] = "Report",
            ["Retention:Participants:1:ServiceName"] = "measureeval",
            ["Retention:Participants:1:Scope"] = "Report"
        });

        Assert.False(participants.IsValid);
        Assert.Equal("Retention:Participants:1:ServiceName 'measureeval' is listed more than once.",
                     Assert.Single(participants.Errors));
    }

    [Fact]
    public void FromConfiguration_NameLongerThanTheRecordStores_IsInvalid()
    {
        var participants = Read(new Dictionary<string, string?>
        {
            ["Retention:Participants:0:ServiceName"] = new string('a', RetentionParticipants.MaxServiceNameLength + 1),
            ["Retention:Participants:0:Scope"] = "Report"
        });

        Assert.False(participants.IsValid);
        Assert.Contains("longer than 100 characters", Assert.Single(participants.Errors));
    }

    [Fact]
    public void FromConfiguration_SeveralProblems_ReportsEachOne()
    {
        var participants = Read(new Dictionary<string, string?>
        {
            ["Retention:Participants:0:Scope"] = "Nope"
        });

        Assert.Equal(2, participants.Errors.Count);
    }

    private static RetentionParticipants Read(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return RetentionParticipants.FromConfiguration(configuration);
    }
}
