using System.Security.Claims;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using Link.Authorization.Infrastructure;
using Link.Authorization.Permissions;

namespace UnitTests.Kafka;

[Trait("Category", "UnitTests")]
public class ChangeRequestWorkflowTests
{
    [Fact]
    public void RequesterCannotApproveOrExecuteOwnRequest()
    {
        var request = Pending();
        Assert.Equal("The requester cannot approve their own request.", ChangeRequestWorkflow.Approve(request, "ada", DateTimeOffset.UnixEpoch));

        request.Status = KafkaChangeStatus.Approved;
        request.Approver = "bea";
        Assert.Equal("The requester cannot execute their own request.", ChangeRequestWorkflow.MarkExecuting(request, "ada", DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void DifferentApprover_ThenExecutor_Advances()
    {
        var request = Pending();
        Assert.Null(ChangeRequestWorkflow.Approve(request, "bea", DateTimeOffset.UnixEpoch));
        Assert.Equal(KafkaChangeStatus.Approved, request.Status);
        Assert.Null(ChangeRequestWorkflow.MarkExecuting(request, "cy", DateTimeOffset.UnixEpoch));
        Assert.Equal(KafkaChangeStatus.Executing, request.Status);
        Assert.Equal("bea", request.Approver);
    }

    [Fact]
    public void OptionalSecondApprover_LetsRequesterExecute()
    {
        var request = Pending();
        request.SecondApproverRequired = false;
        request.Status = KafkaChangeStatus.Approved;
        Assert.Null(ChangeRequestWorkflow.MarkExecuting(request, "ada", DateTimeOffset.UnixEpoch));
        Assert.Equal("ada", request.Approver);
    }

    [Fact]
    public void Reject_RequiresAReason_AndCloses()
    {
        var request = Pending();
        Assert.NotNull(ChangeRequestWorkflow.Reject(request, "bea", " ", DateTimeOffset.UnixEpoch));
        Assert.Null(ChangeRequestWorkflow.Reject(request, "bea", "not now", DateTimeOffset.UnixEpoch));
        Assert.Equal(KafkaChangeStatus.Rejected, request.Status);
        Assert.NotNull(request.ClosedUtc);
    }

    [Fact]
    public void IsLinkAdmin_DoesNotGrantKafkaPermissions()
    {
        var admin = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, nameof(LinkSystemPermissions.IsLinkAdmin))
        }, "test"));

        Assert.False(KafkaOpsService.Has(admin, LinkSystemPermissions.CanViewInfrastructure));
        Assert.False(KafkaOpsService.Has(admin, LinkSystemPermissions.CanManageKafkaTopics));
        Assert.False(KafkaOpsService.Has(admin, LinkSystemPermissions.CanManageScaling));

        var viewer = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, nameof(LinkSystemPermissions.CanViewInfrastructure))
        }, "test"));
        Assert.True(KafkaOpsService.Has(viewer, LinkSystemPermissions.CanViewInfrastructure));
    }

    [Fact]
    public void AccountClaimCatalog_IncludesTheKafkaPermissions()
    {
        var names = LinkPermissionsProvider.GetLinkPermissions().Select(permission => permission.ToString()).ToList();

        Assert.Contains(nameof(LinkSystemPermissions.CanViewInfrastructure), names);
        Assert.Contains(nameof(LinkSystemPermissions.CanManageKafkaTopics), names);
        Assert.Contains(nameof(LinkSystemPermissions.CanManageScaling), names);
    }

    [Fact]
    public void SeedRoles_NamesTheKafkaClaims()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        string? path = null;
        while (root is not null && path is null)
        {
            var candidate = Path.Combine(root.FullName, "DotNet", "Account", "Persistence", "Extensions", "ModelBuilderExtensions.cs");
            if (File.Exists(candidate))
                path = candidate;
            root = root.Parent;
        }

        Assert.NotNull(path);
        var text = File.ReadAllText(path);
        Assert.Contains("nameof(LinkSystemPermissions.CanViewInfrastructure)", text, StringComparison.Ordinal);
        Assert.Contains("nameof(LinkSystemPermissions.CanManageKafkaTopics)", text, StringComparison.Ordinal);
        Assert.Contains("nameof(LinkSystemPermissions.CanManageScaling)", text, StringComparison.Ordinal);
    }

    private static ChangeRequestRecord Pending() => new()
    {
        Status = KafkaChangeStatus.Pending,
        SecondApproverRequired = true,
        Requester = "ada"
    };
}
