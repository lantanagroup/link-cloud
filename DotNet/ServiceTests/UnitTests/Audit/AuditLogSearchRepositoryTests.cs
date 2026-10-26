using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Persistance;
using LantanaGroup.Link.Audit.Persistance.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Audit
{
    [Trait("Category", "UnitTests")]
    public class AuditLogSearchRepositoryTests
    {
        private const string Facility = "TestFacility";

        private static AuditLogSearchRepository BuildRepository(out AuditDbContext dbContext)
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase($"AuditLogSearchRepositoryTests_{Guid.NewGuid():N}")
                .Options;

            dbContext = new AuditDbContext(options);
            return new AuditLogSearchRepository(new Mock<ILogger<AuditLogRepository>>().Object, dbContext);
        }

        private static AuditLog Log(string action, string resource, string? resourceId, string? patientId, string facility = Facility) => new()
        {
            AuditId = AuditId.NewId(),
            FacilityId = facility,
            ServiceName = "TestService",
            Action = action,
            Resource = resource,
            ResourceId = resourceId,
            PatientId = patientId,
            EventDate = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow
        };

        private static async Task<AuditLogSearchRepository> SeededRepository()
        {
            var repository = BuildRepository(out var dbContext);

            dbContext.AuditLogs.AddRange(
                Log("Update", "ReportingPlan", "plan-1", "patient-1"),
                Log("Update", "ReportingPlan", "plan-2", "patient-2"),
                //a bulk action: the resource type is set but no single record is identified
                Log("Delete", "ReportingPlan", null, null),
                Log("Create", "FhirQueryConfiguration", "config-1", null),
                Log("SoftDelete", "ReportSchedule", "schedule-1", "patient-1", "OtherFacility"));

            await dbContext.SaveChangesAsync();
            return repository;
        }

        private static Task<(IEnumerable<AuditLog>, LantanaGroup.Link.Audit.Application.Models.PaginationMetadata)> Search(
            AuditLogSearchRepository repository,
            string? facility = null,
            string? patientId = null,
            string? resource = null,
            string? resourceId = null,
            int pageSize = 10)
            => repository.SearchAsync(null, facility, null, null, null, null, patientId, resource, resourceId, null, null, pageSize, 1);

        [Fact]
        public async Task SearchAsync_FiltersByPatientId()
        {
            var repository = await SeededRepository();

            var (logs, metadata) = await Search(repository, patientId: "patient-1");

            Assert.Equal(2, metadata.TotalCount);
            Assert.All(logs, log => Assert.Equal("patient-1", log.PatientId));
        }

        [Fact]
        public async Task SearchAsync_UnknownPatientId_ReturnsEmptyWithMetadata()
        {
            var repository = await SeededRepository();

            var (logs, metadata) = await Search(repository, patientId: "no-such-patient");

            Assert.Empty(logs);
            Assert.Equal(0, metadata.TotalCount);
            Assert.Equal(10, metadata.PageSize);
            Assert.Equal(1, metadata.PageNumber);
        }

        [Fact]
        public async Task SearchAsync_FiltersByResourceId()
        {
            var repository = await SeededRepository();

            var (logs, metadata) = await Search(repository, resourceId: "plan-2");

            Assert.Equal(1, metadata.TotalCount);
            Assert.Equal("patient-2", Assert.Single(logs).PatientId);
        }

        [Fact]
        public async Task SearchAsync_FiltersByResourceAlone_IncludesTheBulkEventWithNoResourceId()
        {
            var repository = await SeededRepository();

            var (logs, metadata) = await Search(repository, resource: "ReportingPlan");

            Assert.Equal(3, metadata.TotalCount);
            Assert.Contains(logs, log => log.ResourceId is null);
        }

        [Fact]
        public async Task SearchAsync_FiltersByResourceAndResourceId_ReturnsOnlyTheMatch()
        {
            var repository = await SeededRepository();

            var (logs, metadata) = await Search(repository, resource: "ReportingPlan", resourceId: "plan-1");

            Assert.Equal(1, metadata.TotalCount);
            Assert.Equal("plan-1", Assert.Single(logs).ResourceId);
        }

        [Fact]
        public async Task SearchAsync_ResourceAndResourceIdThatDoNotAgree_ReturnsEmpty()
        {
            var repository = await SeededRepository();

            var (logs, metadata) = await Search(repository, resource: "FhirQueryConfiguration", resourceId: "plan-1");

            Assert.Empty(logs);
            Assert.Equal(0, metadata.TotalCount);
        }

        [Fact]
        public async Task SearchAsync_CombinesFacilityAndPatientFilters()
        {
            var repository = await SeededRepository();

            var (logs, metadata) = await Search(repository, facility: "OtherFacility", patientId: "patient-1");

            Assert.Equal(1, metadata.TotalCount);
            Assert.Equal("schedule-1", Assert.Single(logs).ResourceId);
        }

        [Fact]
        public async Task SearchAsync_WithoutTheNewFilters_ReturnsEverything()
        {
            var repository = await SeededRepository();

            var (logs, metadata) = await Search(repository);

            Assert.Equal(5, metadata.TotalCount);
            Assert.Equal(5, logs.Count());
        }
    }
}
