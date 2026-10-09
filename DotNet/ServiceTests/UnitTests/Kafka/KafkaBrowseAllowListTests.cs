using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Settings;
using Xunit;

namespace Link.ServiceTests.UnitTests.Kafka;

public class KafkaBrowseAllowListTests
{
    [Fact]
    public void Admit_AllowsCatalogMainErrorRetryAndRedrive()
    {
        var main = KafkaBrowseAllowList.Admit("ResourcesAcquired", null);
        Assert.True(main.Allowed);
        Assert.Equal(KafkaBrowseAllowList.KindMain, main.Kind);
        Assert.Equal("ResourcesAcquired", main.Topic);

        var error = KafkaBrowseAllowList.Admit("ResourcesAcquired-Error", null);
        Assert.True(error.Allowed);
        Assert.Equal(KafkaBrowseAllowList.KindError, error.Kind);

        var retry = KafkaBrowseAllowList.Admit("ResourcesAcquired-Retry", null);
        Assert.True(retry.Allowed);
        Assert.Equal(KafkaBrowseAllowList.KindRetry, retry.Kind);

        var serviceRetry = KafkaBrowseAllowList.Admit("ResourcesAcquired-Retry-Normalization", null);
        Assert.True(serviceRetry.Allowed);
        Assert.Equal(KafkaBrowseAllowList.KindRetry, serviceRetry.Kind);

        var redrive = KafkaBrowseAllowList.Admit("ResourcesAcquired-Redrive-Normalization", null);
        Assert.True(redrive.Allowed);
        Assert.Equal(KafkaBrowseAllowList.KindRedrive, redrive.Kind);
    }

    [Fact]
    public void Admit_RefusesInternalUnknownAndUnlistedServices()
    {
        Assert.False(KafkaBrowseAllowList.Admit("__consumer_offsets", null).Allowed);
        Assert.Contains("Internal", KafkaBrowseAllowList.Admit("__consumer_offsets", null).Reason, StringComparison.Ordinal);

        Assert.False(KafkaBrowseAllowList.Admit("NotAPipelineTopic", null).Allowed);
        Assert.False(KafkaBrowseAllowList.Admit("ResourcesAcquired-Retry-NotAService", null).Allowed);
        Assert.False(KafkaBrowseAllowList.Admit("_linkmig-journal", null).Allowed);
    }

    [Fact]
    public void Admit_AllowsABackupOnlyWhenAMigrationRecordNamesIt()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var backup = KafkaTopicCatalog.BackupTopicName("ResourcesAcquired", id);
        Assert.False(KafkaBrowseAllowList.Admit(backup, []).Allowed);
        var allowed = KafkaBrowseAllowList.Admit(backup, [backup]);
        Assert.True(allowed.Allowed);
        Assert.Equal(KafkaBrowseAllowList.KindBackup, allowed.Kind);
        Assert.Equal("ResourcesAcquired", allowed.Main);
    }

    [Fact]
    public void Decode_ReadsFacilityOnlyJsonPlainKeysCompositesAndHealthKeys()
    {
        var facilityOnly = KafkaBrowseDecoder.Decode("ReportScheduled", "{\"facilityId\":\"fac-1\"}", null, null);
        Assert.Equal("fac-1", facilityOnly.FacilityId);
        Assert.Null(facilityOnly.PatientId);

        var plain = KafkaBrowseDecoder.Decode("ReportScheduled", "fac-2", null, null);
        Assert.Equal("fac-2", plain.FacilityId);
        Assert.Null(plain.PatientId);

        var composite = KafkaBrowseDecoder.Decode("ReadyToAcquire", "fac-3|pat-3", null, null);
        Assert.Equal("fac-3", composite.FacilityId);
        Assert.Equal("pat-3", composite.PatientId);

        var health = KafkaBrowseDecoder.Decode("Service-Healthcheck", "DataAcquisition", null, null);
        Assert.Null(health.FacilityId);
        Assert.Null(health.PatientId);

        var brokenJson = KafkaBrowseDecoder.Decode("ReportScheduled", "{not-json", null, null);
        Assert.Null(brokenJson.FacilityId);
    }

    [Fact]
    public void Decode_ReadsReportIdsAndExceptionHeaders()
    {
        var headers = new[]
        {
            new KafkaBrowseHeader { Name = KafkaConstants.HeaderConstants.CorrelationId, Value = "corr-1" },
            new KafkaBrowseHeader { Name = KafkaConstants.HeaderConstants.RetryCount, Value = "2" },
            new KafkaBrowseHeader { Name = KafkaConstants.HeaderConstants.ExceptionService, Value = "Report" },
            new KafkaBrowseHeader { Name = KafkaConstants.HeaderConstants.ExceptionMessage, Value = "boom" }
        };
        var link = KafkaBrowseDecoder.Decode(
            "ReportScheduled",
            "{\"facilityId\":\"fac-9\"}",
            "{\"reportId\":\"rep-9\",\"patientId\":\"pat-9\"}",
            headers);

        Assert.Equal("fac-9", link.FacilityId);
        Assert.Equal("pat-9", link.PatientId);
        Assert.Equal("rep-9", link.ReportId);
        Assert.Equal("corr-1", link.CorrelationId);
        Assert.Equal("2", link.RetryCount);
        Assert.Equal("Report", link.ExceptionService);
        Assert.Equal("boom", link.ExceptionMessage);
    }
}
