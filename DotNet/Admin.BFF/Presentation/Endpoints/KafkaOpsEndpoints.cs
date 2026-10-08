using System.Security.Claims;
using System.Text.RegularExpressions;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints;

public sealed class KafkaOpsEndpoints(IKafkaOpsService kafkaOps, ILogger<KafkaOpsEndpoints> logger) : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/ops/kafka")
            .WithTags("Kafka operations");

        group.MapGet("/topics", GetTopics)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapGet("/groups", GetGroups)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapGet("/capabilities", GetCapabilities)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapPost("/topics/{topic}/partitions/plan", Plan)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapGet("/cluster", GetCluster)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapGet("/infra", GetInfra)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapPost("/groups/{groupId}/replicas/plan", PlanScale)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapPost("/groups/{groupId}/replicas", CreateScale)
            .RequireAuthorization(PolicyNames.CanManageScaling);
        group.MapPost("/brokers/plan", PlanAddBroker)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapPost("/brokers", CreateAddBroker)
            .RequireAuthorization(PolicyNames.CanManageScaling);
        group.MapPost("/brokers/{brokerId:int}/decommission/plan", PlanDecommission)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapPost("/brokers/{brokerId:int}/decommission", CreateDecommission)
            .RequireAuthorization(PolicyNames.CanManageScaling);
        group.MapPost("/brokers/{brokerId:int}/rebalance/plan", PlanRebalance)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapPost("/brokers/{brokerId:int}/rebalance", CreateRebalance)
            .RequireAuthorization(PolicyNames.CanManageScaling);
        group.MapPost("/change-requests", Create)
            .RequireAuthorization(PolicyNames.CanManageKafkaTopics);
        group.MapGet("/change-requests/{id:guid}", Get)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapPost("/change-requests/{id:guid}/approve", Approve)
            .RequireAuthorization(PolicyNames.CanOperateKafka);
        group.MapPost("/change-requests/{id:guid}/reject", Reject)
            .RequireAuthorization(PolicyNames.CanOperateKafka);
        group.MapPost("/change-requests/{id:guid}/execute", Execute)
            .RequireAuthorization(PolicyNames.CanOperateKafka);
        group.MapPost("/change-requests/{id:guid}/cancel", Cancel)
            .RequireAuthorization(PolicyNames.CanOperateKafka);
    }

    private async Task<IResult> GetTopics(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        return Results.Ok(await kafkaOps.GetTopicsAsync(cancellationToken));
    }

    private async Task<IResult> GetGroups(ClaimsPrincipal user, CancellationToken cancellationToken, bool includeTestGroups = false)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        return Results.Ok(await kafkaOps.GetGroupsAsync(includeTestGroups, cancellationToken));
    }

    private async Task<IResult> GetCapabilities(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        return Results.Ok(await kafkaOps.GetCapabilitiesAsync(cancellationToken));
    }

    private async Task<IResult> Plan(ClaimsPrincipal user, string topic, PartitionPlanBody body, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        if (!TryTopic(topic, out var name, out var invalid))
            return Problem(invalid, StatusCodes.Status400BadRequest);
        try
        {
            var plan = await kafkaOps.PlanAsync(name, body.Partitions, body.OverrideQuietWindow, body.OverrideReason, cancellationToken);
            return plan.Accepted ? Results.Ok(plan) : Results.BadRequest(plan);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return Problem(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private async Task<IResult> Create(ClaimsPrincipal user, HttpContext http, ChangeRequestBody body, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanManage(user))
            return Results.Forbid();
        if (!TryTopic(body.Topic, out var name, out var invalid))
            return Problem(invalid, StatusCodes.Status400BadRequest);
        try
        {
            var correlation = http.Request.Headers["X-Correlation-Id"].FirstOrDefault();
            var record = await kafkaOps.CreateAsync(user, name, body.Partitions, body.Reason ?? "", body.OverrideQuietWindow, body.OverrideReason, body.Confirmation, correlation, cancellationToken);
            return Results.Created($"/api/ops/kafka/change-requests/{record.Id}", record);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return Problem(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private async Task<IResult> Get(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        var record = await kafkaOps.GetAsync(id, cancellationToken);
        return record is null ? Results.NotFound() : Results.Ok(record);
    }

    private async Task<IResult> GetCluster(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        return Results.Ok(await kafkaOps.GetClusterAsync(cancellationToken));
    }

    private IResult GetInfra(ClaimsPrincipal user)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        return Results.Ok(kafkaOps.Infra);
    }

    private async Task<IResult> PlanScale(ClaimsPrincipal user, string groupId, ScaleBody body, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        if (!TryToken(groupId, out var name))
            return Problem("Group id is invalid.", StatusCodes.Status400BadRequest);
        try
        {
            var plan = await kafkaOps.PlanScaleAsync(name, body.Replicas, cancellationToken);
            return plan.Accepted ? Results.Ok(plan) : Results.BadRequest(plan);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return Problem(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private async Task<IResult> CreateScale(ClaimsPrincipal user, string groupId, ScaleBody body, HttpContext http, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanScale(user))
            return Results.Forbid();
        if (!TryToken(groupId, out var name))
            return Problem("Group id is invalid.", StatusCodes.Status400BadRequest);
        try
        {
            var record = await kafkaOps.CreateScaleAsync(user, name, body.Replicas, body.Reason ?? "", Correlation(http), cancellationToken);
            return Results.Created($"/api/ops/kafka/change-requests/{record.Id}", record);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return StatusFor(ex);
        }
    }

    private async Task<IResult> PlanAddBroker(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        return Results.Ok(kafkaOps.Infra);
    }

    private async Task<IResult> CreateAddBroker(ClaimsPrincipal user, ReasonBody body, HttpContext http, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanScale(user))
            return Results.Forbid();
        try
        {
            var record = await kafkaOps.CreateAddBrokerAsync(user, body.Reason ?? "", Correlation(http), cancellationToken);
            return Results.Created($"/api/ops/kafka/change-requests/{record.Id}", record);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return StatusFor(ex);
        }
    }

    private async Task<IResult> PlanDecommission(ClaimsPrincipal user, int brokerId, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        try
        {
            var plan = await kafkaOps.PlanDecommissionAsync(brokerId, cancellationToken);
            return plan.Accepted ? Results.Ok(plan) : Results.BadRequest(plan);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return Problem(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private async Task<IResult> CreateDecommission(ClaimsPrincipal user, int brokerId, ReasonBody body, HttpContext http, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanScale(user))
            return Results.Forbid();
        try
        {
            var record = await kafkaOps.CreateDecommissionAsync(user, brokerId, body.Reason ?? "", Correlation(http), cancellationToken);
            return Results.Created($"/api/ops/kafka/change-requests/{record.Id}", record);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return StatusFor(ex);
        }
    }

    private async Task<IResult> PlanRebalance(ClaimsPrincipal user, int brokerId, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        try
        {
            var plan = await kafkaOps.PlanRebalanceAsync(brokerId, cancellationToken);
            return plan.Accepted ? Results.Ok(plan) : Results.BadRequest(plan);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return Problem(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private async Task<IResult> CreateRebalance(ClaimsPrincipal user, int brokerId, ReasonBody body, HttpContext http, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanScale(user))
            return Results.Forbid();
        try
        {
            var record = await kafkaOps.CreateRebalanceAsync(user, brokerId, body.Reason ?? "", Correlation(http), cancellationToken);
            return Results.Created($"/api/ops/kafka/change-requests/{record.Id}", record);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return StatusFor(ex);
        }
    }

    private async Task<IResult> Cancel(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken) =>
        await Mutate(user, () => kafkaOps.CancelAsync(user, id, cancellationToken));

    private static string? Correlation(HttpContext http) =>
        http.Request.Headers["X-Correlation-Id"].FirstOrDefault();

    private IResult StatusFor(KafkaOpsRejectedException ex)
    {
        var forbidden = ex.Message.Contains("read-only", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("not allowed", StringComparison.OrdinalIgnoreCase);
        return Problem(ex.Message, forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status400BadRequest);
    }

    private static bool TryToken(string? value, out string name)
    {
        name = (value ?? "").Trim();
        if (name.Length == 0 || name.Length > 249 || !TopicName.IsMatch(name))
            return false;
        return true;
    }

    private async Task<IResult> Approve(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken) =>
        await Mutate(user, () => kafkaOps.ApproveAsync(user, id, cancellationToken));

    private async Task<IResult> Reject(ClaimsPrincipal user, Guid id, RejectBody body, CancellationToken cancellationToken) =>
        await Mutate(user, () => kafkaOps.RejectAsync(user, id, body.Reason ?? "", cancellationToken));

    private async Task<IResult> Execute(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        var existing = await kafkaOps.GetAsync(id, cancellationToken);
        if (existing is null)
            return Problem("That change request was not found or it has expired.", StatusCodes.Status404NotFound);
        if (!KafkaOpsExecution.Allows(kafkaOps.CanManage(user), kafkaOps.CanScale(user), existing.Kind))
            return Results.Forbid();
        try
        {
            var record = await kafkaOps.ExecuteAsync(user, id, cancellationToken);
            return Results.Accepted($"/api/ops/kafka/change-requests/{record.Id}", record);
        }
        catch (KafkaOpsNotFoundException ex)
        {
            return Problem(ex.Message, StatusCodes.Status404NotFound);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return StatusFor(ex);
        }
    }

    private async Task<IResult> Mutate(ClaimsPrincipal user, Func<Task<ChangeRequestRecord>> action)
    {
        if (!kafkaOps.CanManage(user) && !kafkaOps.CanScale(user))
            return Results.Forbid();
        try
        {
            var record = await action();
            return Results.Ok(record);
        }
        catch (KafkaOpsNotFoundException ex)
        {
            return Problem(ex.Message, StatusCodes.Status404NotFound);
        }
        catch (KafkaOpsRejectedException ex)
        {
            var status = ex.Message.Contains("read-only", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status400BadRequest;
            return Problem(ex.Message, status);
        }
    }

    private IResult Problem(string detail, int status)
    {
        logger.LogInformation("Kafka ops request refused: {Detail}", detail.Sanitize());
        return Results.Problem(detail: detail, statusCode: status);
    }

    private static readonly Regex TopicName = new("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static bool TryTopic(string? topic, out string name, out string error)
    {
        name = (topic ?? "").SanitizeAndRemove();
        if (name.Length == 0 || name.Length > 249 || !TopicName.IsMatch(name))
        {
            error = "Topic name is invalid.";
            return false;
        }

        error = "";
        return true;
    }
}

public sealed class PartitionPlanBody
{
    public int Partitions { get; set; }
    public bool OverrideQuietWindow { get; set; }
    public string? OverrideReason { get; set; }
}

public sealed class ChangeRequestBody
{
    public string? Topic { get; set; }
    public int Partitions { get; set; }
    public string? Reason { get; set; }
    public bool OverrideQuietWindow { get; set; }
    public string? OverrideReason { get; set; }
    public string? Confirmation { get; set; }
}

public sealed class ScaleBody
{
    public int Replicas { get; set; }
    public string? Reason { get; set; }
}

public sealed class ReasonBody
{
    public string? Reason { get; set; }
}

public sealed class RejectBody
{
    public string? Reason { get; set; }
}
