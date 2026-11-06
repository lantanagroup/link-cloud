using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Controllers;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

/// <summary>
/// What the adopted handlers put in the audit context - the part the attribute cannot take from the
/// route: the ids a bulk delete removed, and the values its note names.
/// </summary>
/// <remarks>
/// The controllers reach the context through HttpContext, so a controller built the way the other
/// tests build it sees no context at all and runs none of this.
/// </remarks>
[Trait("Category", "UnitTests")]
public class NormalizationAuditContextTests
{
    private static (ControllerContext Context, IAuditContext Audit) Recording()
    {
        var audit = new AuditContext { IsRecording = true };
        var services = new ServiceCollection();
        services.AddSingleton<IAuditContext>(audit);

        return (new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() }
        }, audit);
    }

    private static OperationsController Operations(IOperationManager manager) =>
        new(manager, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

    [Fact]
    public async Task DeleteFacilityOperations_RecordsTheIdsItDeleted()
    {
        IReadOnlyList<Guid> deleted = [Guid.NewGuid(), Guid.NewGuid()];
        var manager = new Mock<IOperationManager>();
        manager.Setup(candidate => candidate.DeleteOperation(It.IsAny<DeleteOperationModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deleted);
        var controller = Operations(manager.Object);
        var (context, audit) = Recording();
        controller.ControllerContext = context;

        var result = await controller.DeleteFacilityOperations("facility", cancellationToken: CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(string.Join(", ", deleted), (string?)audit.Values["ids"]);

        //several rows went, so none of them is the single row the event is about.
        Assert.Null(audit.RecordId);
    }

    [Fact]
    public async Task DeleteFacilityOperations_OneRow_NamesItAsTheResource()
    {
        var operationId = Guid.NewGuid();
        IReadOnlyList<Guid> deleted = [operationId];
        var manager = new Mock<IOperationManager>();
        manager.Setup(candidate => candidate.DeleteOperation(It.IsAny<DeleteOperationModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deleted);
        var controller = Operations(manager.Object);
        var (context, audit) = Recording();
        controller.ControllerContext = context;

        await controller.DeleteFacilityOperations("facility", operationId, cancellationToken: CancellationToken.None);

        Assert.Equal(operationId.ToString(), audit.RecordId);
    }

    [Fact]
    public async Task DeleteHSLOCById_MissingRow_DropsTheNote()
    {
        var manager = new Mock<IHSLOCManager>();
        manager.Setup(candidate => candidate.DeleteById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HSLOCDeletedRow?)null);
        var controller = new HSLOCController(manager.Object, Mock.Of<IHSLOCQueries>());
        var (context, audit) = Recording();
        controller.ControllerContext = context;

        await controller.DeleteById(Guid.NewGuid(), CancellationToken.None);

        //the note names a code and a version, and there were none to name.
        Assert.True(audit.SuppressNote);
        Assert.Empty(audit.Values);
    }

    [Fact]
    public async Task DeleteHSLOCById_RecordsTheCodeAndVersionTheNoteNames()
    {
        var manager = new Mock<IHSLOCManager>();
        manager.Setup(candidate => candidate.DeleteById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HSLOCDeletedRow("A1", "2025"));
        var controller = new HSLOCController(manager.Object, Mock.Of<IHSLOCQueries>());
        var (context, audit) = Recording();
        controller.ControllerContext = context;

        await controller.DeleteById(Guid.NewGuid(), CancellationToken.None);

        Assert.False(audit.SuppressNote);
        Assert.Equal("A1", (string?)audit.Values["HSLOCCode"]);
        Assert.Equal("2025", (string?)audit.Values["version"]);
    }
}
