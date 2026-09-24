using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using Moq;
using Moq.AutoMock;
using List = Hl7.Fhir.Model.List;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace ServiceTests.UnitTests.DataAcquisition.Services;

[Trait("Category", "UnitTests")]
public class EhrPatientListServiceTests
{
    private const string FacilityId = "facility-1";
    private const string BaseUrl = "http://ehr.example.com/fhir";

    private static FhirListConfigurationModel ListConfig(params string[] fhirIds) => new()
    {
        FacilityId = FacilityId,
        FhirBaseServerUrl = BaseUrl,
        EHRPatientLists = fhirIds
            .Select(id => new EhrPatientListModel
            {
                FhirId = id,
                Status = ListType.Admit,
                TimeFrame = TimeFrame.LessThan24Hours
            })
            .ToList()
    };

    private static List FhirListWith(params (string? reference, string? display)[] entries) => new()
    {
        Entry = entries
            .Select(e => new List.EntryComponent
            {
                Item = e.reference == null ? null : new ResourceReference(e.reference) { Display = e.display }
            })
            .ToList()
    };

    private static FhirQueryConfigurationModel QueryConfig(string? baseUrl = null) =>
        new() { FacilityId = FacilityId, FhirServerBaseUrl = baseUrl ?? BaseUrl };

    private static void SetupQueryConfig(AutoMocker mocker, FhirQueryConfigurationModel? config)
    {
        mocker.GetMock<IFhirQueryConfigurationQueries>()
            .Setup(q => q.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
    }

    private static EhrPatientListService CreateService(AutoMocker mocker, Func<string, DomainResource?> readFactory)
    {
        SetupQueryConfig(mocker, QueryConfig());
        mocker.GetMock<IReadFhirCommand>()
            .Setup(r => r.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadFhirCommandRequest req, CancellationToken _) => readFactory(req.resourceId)!);
        return mocker.CreateInstance<EhrPatientListService>();
    }

    [Fact]
    public async Task PopulatePatientsAsync_MapsReferenceToIdAndDisplayToName()
    {
        var mocker = new AutoMocker();
        var service = CreateService(mocker, _ => FhirListWith(("Patient/PT-1", "Doe, Jane"), ("Patient/PT-2", "Roe, Sam")));
        var config = ListConfig("list-a");

        await service.PopulatePatientsAsync(config, CancellationToken.None);

        var patients = config.EHRPatientLists[0].Patients;
        Assert.NotNull(patients);
        Assert.Equal(2, patients!.Count);
        Assert.Equal("PT-1", patients[0].Id);
        Assert.Equal("Doe, Jane", patients[0].Name);
        Assert.Equal("PT-2", patients[1].Id);
        Assert.Equal("Roe, Sam", patients[1].Name);
    }

    [Fact]
    public async Task PopulatePatientsAsync_AbsoluteOrBareReference_StripsToBareId()
    {
        var mocker = new AutoMocker();
        var service = CreateService(mocker, _ => FhirListWith(
            ("http://ehr.example.com/fhir/Patient/PT-9", "A"),
            ("PT-10", "B")));
        var config = ListConfig("list-a");

        await service.PopulatePatientsAsync(config, CancellationToken.None);

        Assert.Equal(new[] { "PT-9", "PT-10" }, config.EHRPatientLists[0].Patients!.Select(p => p.Id));
    }

    [Fact]
    public async Task PopulatePatientsAsync_BlankDisplay_YieldsNullName()
    {
        var mocker = new AutoMocker();
        var service = CreateService(mocker, _ => FhirListWith(("Patient/PT-1", "   ")));
        var config = ListConfig("list-a");

        await service.PopulatePatientsAsync(config, CancellationToken.None);

        Assert.Null(config.EHRPatientLists[0].Patients![0].Name);
    }

    [Fact]
    public async Task PopulatePatientsAsync_EntryWithoutItem_YieldsNullIdAndName()
    {
        var mocker = new AutoMocker();
        var service = CreateService(mocker, _ => FhirListWith((null, null), ("Patient/PT-1", "Doe, Jane")));
        var config = ListConfig("list-a");

        await service.PopulatePatientsAsync(config, CancellationToken.None);

        var patients = config.EHRPatientLists[0].Patients!;
        Assert.Equal(2, patients.Count);
        Assert.Null(patients[0].Id);
        Assert.Null(patients[0].Name);
        Assert.Equal("PT-1", patients[1].Id);
    }

    [Fact]
    public async Task PopulatePatientsAsync_ListWithNoEntries_YieldsEmptyNotNull()
    {
        var mocker = new AutoMocker();
        var service = CreateService(mocker, _ => new List());
        var config = ListConfig("list-a");

        await service.PopulatePatientsAsync(config, CancellationToken.None);

        Assert.NotNull(config.EHRPatientLists[0].Patients);
        Assert.Empty(config.EHRPatientLists[0].Patients!);
    }

    [Fact]
    public async Task PopulatePatientsAsync_QueriesEveryConfiguredListInOrder()
    {
        var mocker = new AutoMocker();
        var requested = new System.Collections.Generic.List<string>();
        SetupQueryConfig(mocker, QueryConfig());
        mocker.GetMock<IReadFhirCommand>()
            .Setup(r => r.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadFhirCommandRequest req, CancellationToken _) =>
            {
                requested.Add(req.resourceId);
                return new List();
            });
        var service = mocker.CreateInstance<EhrPatientListService>();
        var config = ListConfig("a", "b", "c", "d", "e", "f");

        await service.PopulatePatientsAsync(config, CancellationToken.None);

        Assert.Equal(new[] { "a", "b", "c", "d", "e", "f" }, requested);
        Assert.All(config.EHRPatientLists, l => Assert.NotNull(l.Patients));
    }

    [Fact]
    public async Task PopulatePatientsAsync_ReadsFromListConfigBaseUrlUsingQueryConfigForAuth()
    {
        var mocker = new AutoMocker();
        var queryConfig = QueryConfig("http://not-used.example.com");
        ReadFhirCommandRequest? captured = null;
        SetupQueryConfig(mocker, queryConfig);
        mocker.GetMock<IReadFhirCommand>()
            .Setup(r => r.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadFhirCommandRequest req, CancellationToken _) => { captured = req; return new List(); });
        var service = mocker.CreateInstance<EhrPatientListService>();

        await service.PopulatePatientsAsync(ListConfig("list-a"), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(FacilityId, captured!.facilityId);
        Assert.Equal(ResourceType.List, captured.resourceType);
        Assert.Equal("list-a", captured.resourceId);
        // Base URL comes from the list configuration; credentials come from the query configuration.
        Assert.Equal(BaseUrl, captured.baseUrl);
        Assert.Same(queryConfig, captured.fhirQueryConfiguration);
    }

    [Fact]
    public async Task PopulatePatientsAsync_NoConfiguredLists_DoesNotCallEhr()
    {
        var mocker = new AutoMocker();
        var service = CreateService(mocker, _ => new List());

        await service.PopulatePatientsAsync(ListConfig(), CancellationToken.None);

        mocker.GetMock<IReadFhirCommand>()
            .Verify(r => r.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PopulatePatientsAsync_MissingFhirQueryConfiguration_ThrowsAndDoesNotCallEhr()
    {
        var mocker = new AutoMocker();
        SetupQueryConfig(mocker, null);
        var service = mocker.CreateInstance<EhrPatientListService>();

        await Assert.ThrowsAsync<MissingFacilityConfigurationException>(
            () => service.PopulatePatientsAsync(ListConfig("list-a"), CancellationToken.None));

        mocker.GetMock<IReadFhirCommand>()
            .Verify(r => r.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PopulatePatientsAsync_BlankFhirBaseServerUrl_Throws()
    {
        var mocker = new AutoMocker();
        var service = CreateService(mocker, _ => new List());
        var config = ListConfig("list-a");
        config.FhirBaseServerUrl = "  ";

        await Assert.ThrowsAsync<MissingFacilityConfigurationException>(
            () => service.PopulatePatientsAsync(config, CancellationToken.None));
    }

    [Fact]
    public async Task PopulatePatientsAsync_BlankFhirIdOnAList_Throws()
    {
        var mocker = new AutoMocker();
        var service = CreateService(mocker, _ => new List());
        var config = ListConfig("list-a");
        config.EHRPatientLists[0].FhirId = "";

        await Assert.ThrowsAsync<PatientListRetrievalFailedException>(
            () => service.PopulatePatientsAsync(config, CancellationToken.None));
    }

    [Fact]
    public async Task PopulatePatientsAsync_ReadReturnsOperationOutcome_Throws()
    {
        var mocker = new AutoMocker();
        var service = CreateService(mocker, _ => new OperationOutcome());

        await Assert.ThrowsAsync<PatientListRetrievalFailedException>(
            () => service.PopulatePatientsAsync(ListConfig("list-a"), CancellationToken.None));
    }

    [Fact]
    public async Task PopulatePatientsAsync_ReadReturnsNull_Throws()
    {
        var mocker = new AutoMocker();
        var service = CreateService(mocker, _ => null);

        await Assert.ThrowsAsync<PatientListRetrievalFailedException>(
            () => service.PopulatePatientsAsync(ListConfig("list-a"), CancellationToken.None));
    }

    [Fact]
    public async Task PopulatePatientsAsync_ReadThrows_WrapsInPatientListRetrievalFailedException()
    {
        var mocker = new AutoMocker();
        var inner = new InvalidOperationException("boom");
        SetupQueryConfig(mocker, QueryConfig());
        mocker.GetMock<IReadFhirCommand>()
            .Setup(r => r.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(inner);
        var service = mocker.CreateInstance<EhrPatientListService>();

        var ex = await Assert.ThrowsAsync<PatientListRetrievalFailedException>(
            () => service.PopulatePatientsAsync(ListConfig("list-a"), CancellationToken.None));

        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public async Task PopulatePatientsAsync_FirstListFails_LaterListsAreNotRead()
    {
        var mocker = new AutoMocker();
        var reads = 0;
        SetupQueryConfig(mocker, QueryConfig());
        mocker.GetMock<IReadFhirCommand>()
            .Setup(r => r.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadFhirCommandRequest _, CancellationToken __) =>
            {
                reads++;
                throw new InvalidOperationException("boom");
            });
        var service = mocker.CreateInstance<EhrPatientListService>();
        var config = ListConfig("a", "b", "c");

        await Assert.ThrowsAsync<PatientListRetrievalFailedException>(
            () => service.PopulatePatientsAsync(config, CancellationToken.None));

        Assert.Equal(1, reads);
        Assert.All(config.EHRPatientLists, l => Assert.Null(l.Patients));
    }

    [Fact]
    public async Task PopulatePatientsAsync_TooManyRequests_PropagatesUnwrapped()
    {
        var mocker = new AutoMocker();
        SetupQueryConfig(mocker, QueryConfig());
        mocker.GetMock<IReadFhirCommand>()
            .Setup(r => r.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TooManyRequestsException("slow down", TimeSpan.FromSeconds(30)));
        var service = mocker.CreateInstance<EhrPatientListService>();

        var ex = await Assert.ThrowsAsync<TooManyRequestsException>(
            () => service.PopulatePatientsAsync(ListConfig("list-a"), CancellationToken.None));

        Assert.Equal(TimeSpan.FromSeconds(30), ex.RetryAfter);
    }

    [Fact]
    public async Task PopulatePatientsAsync_CancelledToken_PropagatesCancellation()
    {
        var mocker = new AutoMocker();
        var service = CreateService(mocker, _ => new List());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.PopulatePatientsAsync(ListConfig("list-a"), cts.Token));
    }

    [Fact]
    public async Task PopulatePatientsAsync_ForwardsCancellationTokenToReadCommand()
    {
        var mocker = new AutoMocker();
        using var cts = new CancellationTokenSource();
        var service = CreateService(mocker, _ => new List());

        await service.PopulatePatientsAsync(ListConfig("list-a"), cts.Token);

        mocker.GetMock<IReadFhirCommand>()
            .Verify(r => r.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task PopulatePatientsAsync_ListCountOtherThanSix_IsAccepted()
    {
        // Rows written before the exactly-6 POST/PUT rule, or edited directly, must still be readable.
        var mocker = new AutoMocker();
        var service = CreateService(mocker, _ => FhirListWith(("Patient/PT-1", "Doe, Jane")));
        var config = ListConfig("a", "b", "c");

        await service.PopulatePatientsAsync(config, CancellationToken.None);

        Assert.All(config.EHRPatientLists, l => Assert.Single(l.Patients!));
    }
}
