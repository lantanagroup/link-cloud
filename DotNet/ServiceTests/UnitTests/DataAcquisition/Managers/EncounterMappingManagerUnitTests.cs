using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Linq.Expressions;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Managers;

[Trait("Category", "UnitTests")]
public class EncounterMappingManagerUnitTests
{
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<IEntityRepository<EncounterMapping>> _mockMappingRepo;
    private readonly Mock<IEntityRepository<EncounterLocation>> _mockLocationRepo;
    private readonly EncounterMappingManager _manager;

    public EncounterMappingManagerUnitTests()
    {
        _mockDatabase = new Mock<IDatabase>();
        _mockMappingRepo = new Mock<IEntityRepository<EncounterMapping>>();
        _mockLocationRepo = new Mock<IEntityRepository<EncounterLocation>>();

        _mockDatabase.Setup(d => d.EncounterMappingRepository).Returns(_mockMappingRepo.Object);
        _mockDatabase.Setup(d => d.EncounterLocationRepository).Returns(_mockLocationRepo.Object);

        _manager = new EncounterMappingManager(_mockDatabase.Object);
    }

    [Fact]
    public void Constructor_NullDatabase_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new EncounterMappingManager(null!));
    }

    [Fact]
    public async Task CreateAsync_SetsDatesAndMapsToEntity()
    {
        // Arrange
        var model = new CreateEncounterMappingModel
        {
            FacilityId = "Fac1",
            EncounterId = "Enc1",
            PatientId = "Pat1",
            MappedToOrg = true
        };

        EncounterMapping capturedEntity = null!;
        _mockMappingRepo
            .Setup(r => r.AddAsync(It.IsAny<EncounterMapping>()))
            .Callback<EncounterMapping>(e => capturedEntity = e)
            .ReturnsAsync((EncounterMapping e) => e);

        // Act
        await _manager.CreateAsync(model);

        // Assert
        Assert.NotNull(capturedEntity);
        Assert.Equal("Fac1", capturedEntity.FacilityId);
        Assert.Equal("Enc1", capturedEntity.EncounterId);
        Assert.Equal("Pat1", capturedEntity.PatientId);
        Assert.True(capturedEntity.MappedToOrg);
        Assert.NotEqual(default, capturedEntity.CreateDate);
        Assert.Equal(capturedEntity.CreateDate, capturedEntity.ModifiedDate);
        _mockDatabase.Verify(d => d.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DuplicateMapping_ThrowsEntityAlreadyExistsException()
    {
        // Arrange
        var model = new CreateEncounterMappingModel
        {
            FacilityId = "Fac1",
            EncounterId = "Enc1",
            PatientId = "Pat1",
            MappedToOrg = true
        };

        _mockMappingRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<EncounterMapping, bool>>>()))
            .ReturnsAsync(new EncounterMapping { FacilityId = "Fac1", EncounterId = "Enc1" });

        // Act
        var ex = await Assert.ThrowsAsync<EntityAlreadyExistsException>(() => _manager.CreateAsync(model));
        Assert.Equal("An EncounterMapping already exists for FacilityId Fac1 and EncounterId Enc1", ex.Message);

        // Assert
        _mockMappingRepo.Verify(r => r.AddAsync(It.IsAny<EncounterMapping>()), Times.Never);
        _mockDatabase.Verify(d => d.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ConcurrentDuplicateInsert_ThrowsEntityAlreadyExistsException()
    {
        // Arrange: pre-check passes (null), SaveChanges trips the unique constraint,
        // and the re-check then finds the row a concurrent request inserted.
        var model = new CreateEncounterMappingModel
        {
            FacilityId = "Fac1",
            EncounterId = "Enc1",
            PatientId = "Pat1",
            MappedToOrg = true
        };

        _mockMappingRepo.SetupSequence(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<EncounterMapping, bool>>>()))
            .ReturnsAsync((EncounterMapping)null!)                                          // pre-check
            .ReturnsAsync(new EncounterMapping { FacilityId = "Fac1", EncounterId = "Enc1" }); // post-failure re-check

        _mockDatabase.Setup(d => d.SaveChangesAsync())
            .ThrowsAsync(new DbUpdateException("unique constraint", new Exception()));

        // Act
        var ex = await Assert.ThrowsAsync<EntityAlreadyExistsException>(() => _manager.CreateAsync(model));
        Assert.Equal("An EncounterMapping already exists for FacilityId Fac1 and EncounterId Enc1", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_DbUpdateExceptionWithoutDuplicate_Rethrows()
    {
        // Arrange: SaveChanges fails for a non-duplicate reason (e.g. bad FK); the re-check
        // finds no existing mapping, so the original DbUpdateException must propagate.
        var model = new CreateEncounterMappingModel
        {
            FacilityId = "Fac1",
            EncounterId = "Enc1",
            PatientId = "Pat1"
        };

        _mockMappingRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<EncounterMapping, bool>>>()))
            .ReturnsAsync((EncounterMapping)null!);

        _mockDatabase.Setup(d => d.SaveChangesAsync())
            .ThrowsAsync(new DbUpdateException("fk violation", new Exception()));

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => _manager.CreateAsync(model));
    }

    [Fact]
    public async Task UpdateByIdAsync_UpdatesModifiedDate()
    {
        // Arrange
        var existing = new EncounterMapping
        {
            EncounterMappingId = 1,
            CreateDate = DateTime.UtcNow.AddDays(-1),
            ModifiedDate = DateTime.UtcNow.AddDays(-1)
        };

        _mockMappingRepo.Setup(r => r.GetAsync(1)).ReturnsAsync(existing);

        var updateModel = new UpdateEncounterMappingModel { MappedToOrg = true };

        // Act
        await _manager.UpdateByIdAsync(1, updateModel);

        // Assert
        Assert.True(existing.MappedToOrg);
        Assert.True(existing.ModifiedDate > existing.CreateDate);
        _mockMappingRepo.Verify(r => r.Update(existing), Times.Once);
        _mockDatabase.Verify(d => d.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateByIdAsync_PartialUpdate_OnlyUpdatesProvidedFields()
    {
        // Arrange
        var existing = new EncounterMapping
        {
            EncounterMappingId = 1,
            MappedToOrg = false,
            ModifiedDate = DateTime.UtcNow.AddDays(-1)
        };

        _mockMappingRepo.Setup(r => r.GetAsync(1)).ReturnsAsync(existing);

        // Update with null OrganizationLocationMappingIds should NOT touch locations
        var updateModel = new UpdateEncounterMappingModel 
        { 
            MappedToOrg = true,
            OrganizationLocationMappingIds = null 
        };

        // Act
        await _manager.UpdateByIdAsync(1, updateModel);

        // Assert
        Assert.True(existing.MappedToOrg);
        _mockLocationRepo.Verify(r => r.FindAsync(It.IsAny<Expression<Func<EncounterLocation, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_AppliesFiltersCorrectly()
    {
        // Arrange
        var searchModel = new EncounterMappingSearchModel
        {
            FacilityId = "Fac1",
            MappedToOrg = true
        };

        var mockQueries = new EncounterMappingQueries(_mockDatabase.Object);
        var expectedResults = new List<EncounterMapping>
        {
            new EncounterMapping { EncounterMappingId = 1, FacilityId = "Fac1", MappedToOrg = true }
        };

        _mockMappingRepo.Setup(r => r.SearchAsync(It.IsAny<Expression<Func<EncounterMapping, bool>>>(), It.IsAny<string>(), It.IsAny<LantanaGroup.Link.Shared.Application.Enums.SortOrder?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((expectedResults, new LantanaGroup.Link.Shared.Application.Models.Responses.PaginationMetadata { TotalCount = 1, PageSize = 10, PageNumber = 1 }));

        // Act
        var result = await mockQueries.SearchAsync(searchModel, 1, 10);

        // Assert
        Assert.Single(result.Records);
        Assert.Equal("Fac1", result.Records.First().FacilityId);
        _mockMappingRepo.Verify(r => r.SearchAsync(It.IsAny<Expression<Func<EncounterMapping, bool>>>(), It.IsAny<string>(), It.IsAny<LantanaGroup.Link.Shared.Application.Enums.SortOrder?>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
    }
}
