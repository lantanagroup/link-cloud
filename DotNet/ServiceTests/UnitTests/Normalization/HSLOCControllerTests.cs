using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Controllers;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Net;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class HSLOCControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAll_RecordsAvailable_ReturnsOkAndPassesIncludeInactiveFilter(bool includeInactive)
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        var expected = new List<HSLOC> { CreateHSLOC("A1") };
        queries.Setup(query => query.GetAll(includeInactive, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var controller = CreateController(manager, queries);

        var result = await controller.GetAll(includeInactive, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
        queries.Verify(query => query.GetAll(includeInactive, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_QueryThrows_ReturnsInternalServerErrorProblemDetails()
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        queries.Setup(query => query.GetAll(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("query failed"));
        var controller = CreateController(manager, queries);

        var result = await controller.GetAll(false, CancellationToken.None);

        var problem = AssertProblem(result.Result!, HttpStatusCode.InternalServerError);
        Assert.Equal("query failed", problem.Detail);
    }

    [Fact]
    public async Task Update_MissingCsvFile_ReturnsBadRequestWithoutCallingManager()
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        var controller = CreateController(manager, queries);

        var result = await controller.Update(new PutHSLOCModel
        {
            OldVersion = "2025",
            NewVersion = "2026",
            CsvFile = null!
        }, CancellationToken.None);

        var problem = AssertProblem(result, HttpStatusCode.BadRequest);
        Assert.Equal("A non-empty HSLOC CSV file must be provided.", problem.Detail);
        manager.Verify(manager => manager.Update(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ValidModel_PassesVersionsAndCsvToManager()
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        string? uploadedCsv = null;
        manager
            .Setup(manager => manager.Update("2025", "2026", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Stream, CancellationToken>((_, _, csv, _) =>
            {
                using var reader = new StreamReader(csv, Encoding.UTF8, leaveOpen: true);
                uploadedCsv = reader.ReadToEnd();
            })
            .Returns(Task.CompletedTask);
        var controller = CreateController(manager, queries);

        var result = await controller.Update(CreateUpdateModel(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("CDCCode,ShortDescription,HSLOCCode,LongDescription", uploadedCsv);
        manager.Verify(manager => manager.Update("2025", "2026", It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_HtmlVersions_PassesSanitizedVersionsToManager()
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        var controller = CreateController(manager, queries);
        var model = CreateUpdateModel();
        model.OldVersion = "<script>alert(1)</script>2025";
        model.NewVersion = "2026<script>alert(1)</script>";

        var result = await controller.Update(model, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        manager.Verify(manager => manager.Update("2025", "2026", It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(UpdateExceptionCases))]
    public async Task Update_ManagerThrows_ReturnsMappedProblemDetails(Exception exception, HttpStatusCode expectedStatus)
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        manager
            .Setup(manager => manager.Update(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        var controller = CreateController(manager, queries);

        var result = await controller.Update(CreateUpdateModel(), CancellationToken.None);

        var problem = AssertProblem(result, expectedStatus);
        Assert.Equal(exception.Message, problem.Detail);
    }

    public static IEnumerable<object[]> UpdateExceptionCases =>
    [
        [new ArgumentException("invalid CSV"), HttpStatusCode.BadRequest],
        [new InvalidOperationException("conflicting version"), HttpStatusCode.Conflict],
        [new Exception("unexpected failure"), HttpStatusCode.InternalServerError]
    ];

    [Fact]
    public async Task DeleteAll_ManagerSucceeds_ReturnsNoContent()
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        var controller = CreateController(manager, queries);

        var result = await controller.DeleteAll(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        manager.Verify(manager => manager.DeleteAll(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAll_ManagerThrows_ReturnsInternalServerErrorProblemDetails()
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        manager.Setup(manager => manager.DeleteAll(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("delete failed"));
        var controller = CreateController(manager, queries);

        var result = await controller.DeleteAll(CancellationToken.None);

        var problem = AssertProblem(result, HttpStatusCode.InternalServerError);
        Assert.Equal("delete failed", problem.Detail);
    }

    [Fact]
    public async Task DeleteByVersion_MissingVersion_ReturnsBadRequestWithoutCallingManager()
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        var controller = CreateController(manager, queries);

        var result = await controller.DeleteByVersion(" ", CancellationToken.None);

        var problem = AssertProblem(result, HttpStatusCode.BadRequest);
        Assert.Equal("An HSLOC version must be provided.", problem.Detail);
        manager.Verify(manager => manager.DeleteByVersion(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteByVersion_ManagerSucceeds_ReturnsNoContent()
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        var controller = CreateController(manager, queries);

        var result = await controller.DeleteByVersion("2026", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        manager.Verify(manager => manager.DeleteByVersion("2026", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteByVersion_HtmlVersion_PassesSanitizedVersionToManager()
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        var controller = CreateController(manager, queries);

        var result = await controller.DeleteByVersion("2026<script>alert(1)</script>", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        manager.Verify(manager => manager.DeleteByVersion("2026", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteByVersion_ManagerThrows_ReturnsInternalServerErrorProblemDetails()
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        manager.Setup(manager => manager.DeleteByVersion(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("delete failed"));
        var controller = CreateController(manager, queries);

        var result = await controller.DeleteByVersion("2026", CancellationToken.None);

        var problem = AssertProblem(result, HttpStatusCode.InternalServerError);
        Assert.Equal("delete failed", problem.Detail);
    }

    [Fact]
    public async Task DeleteById_EmptyId_ReturnsBadRequestWithoutCallingManager()
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        var controller = CreateController(manager, queries);

        var result = await controller.DeleteById(Guid.Empty, CancellationToken.None);

        var problem = AssertProblem(result, HttpStatusCode.BadRequest);
        Assert.Equal("A valid HSLOC identifier must be provided.", problem.Detail);
        manager.Verify(manager => manager.DeleteById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteById_ManagerSucceeds_ReturnsNoContent()
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        var id = Guid.NewGuid();
        var controller = CreateController(manager, queries);

        var result = await controller.DeleteById(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        manager.Verify(manager => manager.DeleteById(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteById_ManagerThrows_ReturnsInternalServerErrorProblemDetails()
    {
        var manager = new Mock<IHSLOCManager>();
        var queries = new Mock<IHSLOCQueries>();
        manager.Setup(manager => manager.DeleteById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("delete failed"));
        var controller = CreateController(manager, queries);

        var result = await controller.DeleteById(Guid.NewGuid(), CancellationToken.None);

        var problem = AssertProblem(result, HttpStatusCode.InternalServerError);
        Assert.Equal("delete failed", problem.Detail);
    }

    private static HSLOCController CreateController(Mock<IHSLOCManager> manager, Mock<IHSLOCQueries> queries) =>
        new(manager.Object, queries.Object);

    private static PutHSLOCModel CreateUpdateModel()
    {
        var content = Encoding.UTF8.GetBytes("CDCCode,ShortDescription,HSLOCCode,LongDescription");
        return new PutHSLOCModel
        {
            OldVersion = "2025",
            NewVersion = "2026",
            CsvFile = new FormFile(new MemoryStream(content), 0, content.Length, "CsvFile", "hsloc.csv")
        };
    }

    private static HSLOC CreateHSLOC(string hslocCode) => new()
    {
        CDCCode = $"cdc-{hslocCode}",
        ShortDescription = $"short-{hslocCode}",
        HSLOCCode = hslocCode,
        LongDescription = $"long-{hslocCode}",
        Version = "2026"
    };

    private static ProblemDetails AssertProblem(IActionResult result, HttpStatusCode expectedStatus)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)expectedStatus, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal((int)expectedStatus, problem.Status);
        return problem;
    }
}