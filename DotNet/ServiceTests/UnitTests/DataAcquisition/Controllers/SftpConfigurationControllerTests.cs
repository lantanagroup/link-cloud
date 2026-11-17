using FluentValidation;
using FluentValidation.Results;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Controllers;

/// <summary>
/// An ADT configuration that finds patients by MRN search cannot be saved for a facility with no FHIR Query
/// Configuration, because every acquisition run would fail without one.
/// </summary>
[Trait("Category", "UnitTests")]
public class SftpConfigurationControllerTests
{
    private const string FacilityId = "adt-facility";
    private static readonly Guid ConfigurationId = Guid.NewGuid();

    private readonly AutoMocker _mocker = new();

    public SftpConfigurationControllerTests()
    {
        _mocker.GetMock<IValidator<CreateSftpConfigurationModel>>()
            .Setup(v => v.ValidateAsync(It.IsAny<CreateSftpConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _mocker.GetMock<IValidator<SftpConfigurationModel>>()
            .Setup(v => v.ValidateAsync(It.IsAny<SftpConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _mocker.GetMock<ITenantApiService>()
            .Setup(t => t.CheckFacilityExists(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mocker.GetMock<ISftpConfigurationManager>()
            .Setup(m => m.CreateAsync(It.IsAny<SftpConfigurationModel>(), FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SftpConfigurationModel { Id = ConfigurationId, OrganizationId = FacilityId });
        _mocker.GetMock<ISftpConfigurationManager>()
            .Setup(m => m.UpdateAsync(FacilityId, It.IsAny<SftpConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SftpConfigurationModel { Id = ConfigurationId, OrganizationId = FacilityId });
    }

    private void HasFhirQueryConfiguration(bool exists) =>
        _mocker.GetMock<IFhirQueryConfigurationQueries>()
            .Setup(q => q.GetByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists ? new FhirQueryConfigurationModel { FacilityId = FacilityId, FhirServerBaseUrl = "https://fhir.example.com" } : null);

    private SftpConfigurationController Controller()
    {
        var controller = _mocker.CreateInstance<SftpConfigurationController>();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static SftpAcquisitionTypeConfiguration Adt(AdtConfiguration adtConfiguration) => new()
    {
        AcquisitionType = SftpAcquisitionType.Census,
        SubType = SftpAcquisitionSubType.HL7ADT,
        ProcessedDirectory = "/processed",
        AdtConfiguration = adtConfiguration,
    };

    private static readonly AdtConfiguration MrnSearch = new() { MrnIdentifierSystem = "urn:oid:1.2.3" };
    private static readonly AdtConfiguration PatientIdOnEvent = new() { PatientIdLocation = "PID-3[5=FHIR].1" };

    private async Task<ActionResult<SftpConfigurationModel>> Create(params SftpAcquisitionTypeConfiguration[] configurations) =>
        await Controller().CreateSftpConfiguration(FacilityId, new CreateSftpConfigurationModel { AcquisitionConfigurations = [.. configurations] }, CancellationToken.None);

    private async Task<ActionResult<SftpConfigurationModel>> Update(params SftpAcquisitionTypeConfiguration[] configurations) =>
        await Controller().UpdateSftpConfiguration(FacilityId, ConfigurationId.ToString(), new SftpConfigurationModel { Id = ConfigurationId, AcquisitionConfigurations = [.. configurations] }, CancellationToken.None);

    private void AssertRejected(ActionResult<SftpConfigurationModel> result, int index)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("FHIR Query Configuration", problem.Detail);
        Assert.Contains($"AcquisitionConfigurations[{index}].AdtConfiguration.MrnIdentifierSystem", problem.Errors.Keys);

        _mocker.GetMock<ISftpConfigurationManager>().Verify(m => m.CreateAsync(It.IsAny<SftpConfigurationModel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<ISftpConfigurationManager>().Verify(m => m.UpdateAsync(It.IsAny<string>(), It.IsAny<SftpConfigurationModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_MrnSearch_WithoutFhirQueryConfiguration_IsRejected()
    {
        HasFhirQueryConfiguration(false);

        AssertRejected(await Create(new SftpAcquisitionTypeConfiguration { SubType = SftpAcquisitionSubType.CernerCCLExtract }, Adt(MrnSearch)), index: 1);
    }

    [Fact]
    public async Task Update_MrnSearch_WithoutFhirQueryConfiguration_IsRejected()
    {
        HasFhirQueryConfiguration(false);

        AssertRejected(await Update(Adt(MrnSearch)), index: 0);
    }

    [Fact]
    public async Task Create_MrnSearch_WithFhirQueryConfiguration_IsSaved()
    {
        HasFhirQueryConfiguration(true);

        Assert.IsType<CreatedAtActionResult>((await Create(Adt(MrnSearch))).Result);
    }

    [Fact]
    public async Task Update_MrnSearch_WithFhirQueryConfiguration_IsSaved()
    {
        HasFhirQueryConfiguration(true);

        Assert.IsType<AcceptedResult>((await Update(Adt(MrnSearch))).Result);
    }

    [Fact]
    public async Task PatientIdOnTheEvent_NeedsNoFhirQueryConfiguration()
    {
        HasFhirQueryConfiguration(false);

        Assert.IsType<CreatedAtActionResult>((await Create(Adt(PatientIdOnEvent))).Result);
        Assert.IsType<AcceptedResult>((await Update(Adt(PatientIdOnEvent))).Result);
        _mocker.GetMock<IFhirQueryConfigurationQueries>().Verify(q => q.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
