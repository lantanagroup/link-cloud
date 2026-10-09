using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints;

public sealed class KafkaOpsEndpoints(IKafkaOpsService kafkaOps, ILogger<KafkaOpsEndpoints> logger, IMigrationRuntime? migrations = null, IKafkaMessageBrowser? browser = null) : IApi
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
        group.MapPost("/topics/{topic}/family/plan", PlanFamily)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapPost("/topics/{topic}/family", CreateFamily)
            .RequireAuthorization(PolicyNames.CanManageKafkaTopics);
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
        group.MapGet("/topics/{topic}/detail", GetDetail)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapGet("/topics/{topic}/configs", GetConfigs)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapPost("/topics/{topic}/migrations/plan", PlanMigration)
            .RequireAuthorization(PolicyNames.CanMigrateKafkaTopics);
        group.MapPost("/migrations", RequestMigration)
            .RequireAuthorization(PolicyNames.CanMigrateKafkaTopics);
        group.MapGet("/migrations/{id:guid}", GetMigration)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapPost("/migrations/{id:guid}/approve", ApproveMigration)
            .RequireAuthorization(PolicyNames.CanMigrateKafkaTopics);
        group.MapPost("/migrations/{id:guid}/reject", RejectMigration)
            .RequireAuthorization(PolicyNames.CanMigrateKafkaTopics);
        group.MapPost("/migrations/{id:guid}/execute", ExecuteMigration)
            .RequireAuthorization(PolicyNames.CanMigrateKafkaTopics);
        group.MapPost("/migrations/{id:guid}/go", GoMigration)
            .RequireAuthorization(PolicyNames.CanMigrateKafkaTopics);
        group.MapPost("/migrations/{id:guid}/abort", AbortMigration)
            .RequireAuthorization(PolicyNames.CanMigrateKafkaTopics);
        group.MapPost("/migrations/{id:guid}/recover", RecoverMigration)
            .RequireAuthorization(PolicyNames.CanMigrateKafkaTopics);
        group.MapPost("/migrations/{id:guid}/manual-step", ManualStep)
            .RequireAuthorization(PolicyNames.CanMigrateKafkaTopics);
        group.MapGet("/migrations/{id:guid}/runbook", GetRunbook)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapPost("/backups/{name}/delete", DeleteBackup)
            .RequireAuthorization(PolicyNames.CanMigrateKafkaTopics);
        group.MapGet("/holds", GetHolds)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapGet("/topics/{topic}/messages", GetMessages)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapGet("/topics/{topic}/messages/export", ExportMessages)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
        group.MapGet("/topics/{topic}/family", GetBrowseFamily)
            .RequireAuthorization(PolicyNames.CanViewInfrastructure);
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

    private async Task<IResult> PlanFamily(ClaimsPrincipal user, string topic, PartitionPlanBody body, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        if (!TryTopic(topic, out var name, out var invalid))
            return Problem(invalid, StatusCodes.Status400BadRequest);
        try
        {
            var plan = await kafkaOps.PlanFamilyAsync(name, body.OverrideQuietWindow, body.OverrideReason, cancellationToken);
            return plan.Accepted ? Results.Ok(plan) : Results.BadRequest(plan);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return StatusFor(ex);
        }
    }

    private async Task<IResult> CreateFamily(ClaimsPrincipal user, string topic, ChangeRequestBody body, HttpContext http, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanManage(user))
            return Results.Forbid();
        if (!TryTopic(topic, out var name, out var invalid))
            return Problem(invalid, StatusCodes.Status400BadRequest);
        try
        {
            var record = await kafkaOps.CreateFamilyAsync(user, name, body.Reason ?? "", body.OverrideQuietWindow, body.OverrideReason, body.Confirmation, Correlation(http), cancellationToken);
            return Results.Created($"/api/ops/kafka/change-requests/{record.Id}", record);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return StatusFor(ex);
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
            return StatusFor(ex);
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
        try
        {
            return Results.Ok(await kafkaOps.PlanAddBrokerAsync(cancellationToken));
        }
        catch (KafkaOpsRejectedException ex)
        {
            return Problem(ex.Message, StatusCodes.Status400BadRequest);
        }
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
        if (ex is LeaseHeldException or KafkaBrowseBusyException)
            return Conflict(ex.Message);
        return Problem(ex.Message, ex is KafkaOpsForbiddenException
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status400BadRequest);
    }

    private IResult Conflict(string detail)
    {
        logger.LogInformation("Kafka ops request refused: {Detail}", detail.Sanitize());
        return new RetryAfterProblem(detail, 5);
    }

    private sealed class RetryAfterProblem(string detail, int seconds) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            httpContext.Response.Headers.RetryAfter = seconds.ToString();
            await httpContext.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                title = "Conflict",
                status = StatusCodes.Status409Conflict,
                detail
            });
        }
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
        if (kafkaOps.ReadOnly)
            return Problem("Kafka changes are read-only in this environment.", StatusCodes.Status403Forbidden);
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
            return StatusFor(ex);
        }
    }

    private IResult Problem(string detail, int status)
    {
        logger.LogInformation("Kafka ops request refused: {Detail}", detail.Sanitize());
        return Results.Problem(detail: detail, statusCode: status);
    }

    private async Task<IResult> GetDetail(ClaimsPrincipal user, string topic, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        if (migrations is null)
            return Problem("Topic migration is not available.", StatusCodes.Status503ServiceUnavailable);
        if (!TryTopic(topic, out var name, out var invalid))
            return Problem(invalid, StatusCodes.Status400BadRequest);
        return Results.Ok(await migrations.DetailAsync(name, cancellationToken));
    }

    private async Task<IResult> GetConfigs(ClaimsPrincipal user, string topic, string? diff, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        if (migrations is null)
            return Problem("Topic migration is not available.", StatusCodes.Status503ServiceUnavailable);
        if (!TryTopic(topic, out var name, out var invalid))
            return Problem(invalid, StatusCodes.Status400BadRequest);
        var compared = string.IsNullOrWhiteSpace(diff) ? "" : diff.Sanitize();
        return Results.Ok(await migrations.ConfigsAsync(name, compared, cancellationToken));
    }

    private async Task<IResult> PlanMigration(ClaimsPrincipal user, string topic, MigrationRequestBody body, CancellationToken cancellationToken)
    {
        if (!migrations?.CanMigrate(user) ?? !kafkaOps.CanMigrate(user))
            return Results.Forbid();
        if (migrations is null)
            return Problem("Topic migration is not available.", StatusCodes.Status503ServiceUnavailable);
        if (!TryTopic(topic, out var name, out var invalid))
            return Problem(invalid, StatusCodes.Status400BadRequest);
        try
        {
            var plan = await migrations.PlanAsync(name, body.Partitions, body.BackupSkip, body.BackupSkipAcknowledged, body.AcknowledgedGroups, cancellationToken);
            return plan.Accepted ? Results.Ok(plan) : Results.BadRequest(plan);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return StatusFor(ex);
        }
    }

    private Task<IResult> RequestMigration(ClaimsPrincipal user, MigrationRequestBody body, CancellationToken cancellationToken)
    {
        if (migrations is null || !migrations.CanMigrate(user))
            return Task.FromResult(migrations is null
                ? Problem("Topic migration is not available.", StatusCodes.Status503ServiceUnavailable)
                : Results.Forbid());
        return MigrationCall(async () =>
        {
            var record = await migrations.RequestAsync(user, body, cancellationToken);
            return record;
        }, created: true);
    }

    private Task<IResult> GetMigration(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        if (migrations is null)
            return Task.FromResult(Problem("Topic migration is not available.", StatusCodes.Status503ServiceUnavailable));
        if (!migrations.CanView(user) && !migrations.CanMigrate(user))
            return Task.FromResult(Results.Forbid());
        return MigrationCall(() => migrations.GetAsync(user, id, mutate: false, cancellationToken));
    }

    private Task<IResult> ApproveMigration(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken) =>
        Migrate(user, () => migrations!.ApproveAsync(user, id, cancellationToken));

    private Task<IResult> RejectMigration(ClaimsPrincipal user, Guid id, RejectBody body, CancellationToken cancellationToken) =>
        Migrate(user, () => migrations!.RejectAsync(user, id, body.Reason ?? "", cancellationToken));

    private Task<IResult> ExecuteMigration(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken) =>
        Migrate(user, () => migrations!.ExecuteAsync(user, id, cancellationToken), accepted: true);

    private Task<IResult> GoMigration(ClaimsPrincipal user, Guid id, MigrationCommandBody body, CancellationToken cancellationToken) =>
        Migrate(user, () => migrations!.CommandAsync(user, id, MigrationCommand.Go, body.Confirmation, cancellationToken));

    private Task<IResult> AbortMigration(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken) =>
        Migrate(user, () => migrations!.CommandAsync(user, id, MigrationCommand.Abort, null, cancellationToken));

    private Task<IResult> RecoverMigration(ClaimsPrincipal user, Guid id, MigrationCommandBody body, CancellationToken cancellationToken)
    {
        var command = (body.Action ?? "").Trim().ToLowerInvariant() switch
        {
            "forward" => MigrationCommand.RecoverForward,
            "deleteforeign" or "delete-foreign" or "foreign" => MigrationCommand.DeleteForeign,
            _ => MigrationCommand.RecoverOriginal
        };
        return Migrate(user, () => migrations!.CommandAsync(user, id, command, body.Confirmation, cancellationToken));
    }

    private Task<IResult> ManualStep(ClaimsPrincipal user, Guid id, ManualStepBody body, CancellationToken cancellationToken) =>
        Migrate(user, () => migrations!.ManualStepAsync(user, id, (body.Workload ?? "").SanitizeAndRemove(), cancellationToken));

    private async Task<IResult> GetRunbook(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        if (migrations is null)
            return Problem("Topic migration is not available.", StatusCodes.Status503ServiceUnavailable);
        if (!migrations.CanView(user) && !migrations.CanMigrate(user))
            return Results.Forbid();
        try
        {
            var text = await migrations.RunbookAsync(user, id, cancellationToken);
            return Results.Text(text, "text/plain");
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

    private async Task<IResult> DeleteBackup(ClaimsPrincipal user, string name, BackupDeleteBody body, CancellationToken cancellationToken)
    {
        if (migrations is null || !migrations.CanMigrate(user))
            return migrations is null
                ? Problem("Topic migration is not available.", StatusCodes.Status503ServiceUnavailable)
                : Results.Forbid();
        if (!TryTopic(name, out var backup, out var invalid))
            return Problem(invalid, StatusCodes.Status400BadRequest);
        try
        {
            await migrations.DeleteBackupAsync(user, backup, (body.Confirmation ?? "").SanitizeAndRemove(), cancellationToken);
            return Results.NoContent();
        }
        catch (KafkaOpsRejectedException ex)
        {
            return StatusFor(ex);
        }
    }

    private async Task<IResult> GetHolds(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!kafkaOps.CanView(user))
            return Results.Forbid();
        if (migrations is null)
            return Results.Ok(Array.Empty<string>());
        return Results.Ok(await migrations.HoldsAsync(cancellationToken));
    }

    private Task<IResult> GetMessages(
        ClaimsPrincipal user,
        string topic,
        string? mode,
        long? offset,
        long? timestamp,
        int? limit,
        string? key,
        string? headerName,
        string? headerValue,
        string? resume,
        long? until,
        string? correlation,
        [FromQuery] int[]? partition,
        CancellationToken cancellationToken) =>
        Browse(user, topic, request => browser!.ReadAsync(request, cancellationToken), mode, offset, timestamp, limit, key, headerName, headerValue, partition, cancellationToken, resume, until, correlation);

    private async Task<IResult> ExportMessages(
        ClaimsPrincipal user,
        string topic,
        string? mode,
        long? offset,
        long? timestamp,
        int? limit,
        string? key,
        string? headerName,
        string? headerValue,
        string? resume,
        long? until,
        string? correlation,
        [FromQuery] int[]? partition,
        CancellationToken cancellationToken)
    {
        var denied = ViewDenied(user);
        if (denied is not null)
            return denied;
        if (browser is null)
            return Problem("Message browse is not available.", StatusCodes.Status503ServiceUnavailable);
        if (!TryTopic(topic, out var name, out var invalid))
            return Problem(invalid, StatusCodes.Status400BadRequest);
        try
        {
            var page = await browser.ExportAsync(user, BrowseRequest(name, mode, offset, timestamp, limit, key, headerName, headerValue, partition, resume, until, correlation), cancellationToken);
            var json = JsonSerializer.Serialize(page.Records, ExportJson);
            return Results.File(Encoding.UTF8.GetBytes(json), "application/json", name + "-messages.json");
        }
        catch (KafkaBrowseBrokerException ex)
        {
            return Problem(ex.Message, StatusCodes.Status502BadGateway);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return StatusFor(ex);
        }
    }

    private Task<IResult> GetBrowseFamily(ClaimsPrincipal user, string topic, CancellationToken cancellationToken)
    {
        var denied = ViewDenied(user);
        if (denied is not null)
            return Task.FromResult(denied);
        if (browser is null)
            return Task.FromResult(Problem("Message browse is not available.", StatusCodes.Status503ServiceUnavailable));
        if (!TryTopic(topic, out var name, out var invalid))
            return Task.FromResult(Problem(invalid, StatusCodes.Status400BadRequest));
        return FamilyCall(name, cancellationToken);
    }

    private async Task<IResult> FamilyCall(string topic, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await browser!.FamilyAsync(topic, cancellationToken));
        }
        catch (KafkaBrowseBrokerException ex)
        {
            return Problem(ex.Message, StatusCodes.Status502BadGateway);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return StatusFor(ex);
        }
    }

    private async Task<IResult> Browse(
        ClaimsPrincipal user,
        string topic,
        Func<KafkaBrowseRequest, Task<KafkaBrowsePage>> read,
        string? mode,
        long? offset,
        long? timestamp,
        int? limit,
        string? key,
        string? headerName,
        string? headerValue,
        int[]? partition,
        CancellationToken cancellationToken,
        string? resume = null,
        long? until = null,
        string? correlation = null)
    {
        var denied = ViewDenied(user);
        if (denied is not null)
            return denied;
        if (browser is null)
            return Problem("Message browse is not available.", StatusCodes.Status503ServiceUnavailable);
        if (!TryTopic(topic, out var name, out var invalid))
            return Problem(invalid, StatusCodes.Status400BadRequest);
        try
        {
            var page = await read(BrowseRequest(name, mode, offset, timestamp, limit, key, headerName, headerValue, partition, resume, until, correlation));
            return Results.Ok(page);
        }
        catch (KafkaBrowseBrokerException ex)
        {
            return Problem(ex.Message, StatusCodes.Status502BadGateway);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return StatusFor(ex);
        }
    }

    private static KafkaBrowseRequest BrowseRequest(
        string topic,
        string? mode,
        long? offset,
        long? timestamp,
        int? limit,
        string? key,
        string? headerName,
        string? headerValue,
        int[]? partition,
        string? resume = null,
        long? until = null,
        string? correlation = null)
    {
        var cleanMode = (mode ?? "").SanitizeAndRemove().Trim().ToLowerInvariant();
        return new KafkaBrowseRequest
        {
            Topic = topic,
            Mode = cleanMode.Length == 0 ? "newest" : cleanMode,
            Partitions = partition ?? [],
            Offset = offset,
            TimestampUnixMs = timestamp,
            UntilUnixMs = until,
            Limit = limit ?? KafkaBrowseLimits.DefaultLimit,
            Key = CleanKey(key),
            HeaderName = (headerName ?? "").SanitizeAndRemove(),
            HeaderValue = (headerValue ?? "").SanitizeAndRemove(),
            Resume = KafkaBrowseResume.Format(KafkaBrowseResume.Parse(resume)),
            CorrelationId = (correlation ?? "").SanitizeAndRemove()
        };
    }

    private static string CleanKey(string? key)
    {
        var raw = key ?? "";
        if (!LinkMessageKey.TryDeserialize(raw, out var parsed) || parsed is null)
            return raw.SanitizeAndRemove();
        var facility = parsed.FacilityId.SanitizeAndRemove();
        if (facility.Length == 0)
            return "";
        var patient = string.IsNullOrEmpty(parsed.PatientId) ? null : parsed.PatientId.SanitizeAndRemove();
        if (patient is { Length: 0 })
            patient = null;
        return new LinkMessageKey(facility, patient).Serialize();
    }

    private IResult? ViewDenied(ClaimsPrincipal user)
    {
        if (kafkaOps.CanView(user))
            return null;
        if (user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();
        return Results.Forbid();
    }

    private static readonly JsonSerializerOptions ExportJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private Task<IResult> Migrate(ClaimsPrincipal user, Func<Task<MigrationRecord>> action, bool accepted = false)
    {
        if (migrations is null || !migrations.CanMigrate(user))
            return Task.FromResult(migrations is null
                ? Problem("Topic migration is not available.", StatusCodes.Status503ServiceUnavailable)
                : Results.Forbid());
        return MigrationCall(action, accepted: accepted);
    }

    private async Task<IResult> MigrationCall(Func<Task<MigrationRecord>> action, bool created = false, bool accepted = false)
    {
        try
        {
            var record = await action();
            if (created)
                return Results.Created($"/api/ops/kafka/migrations/{record.Id}", record);
            if (accepted)
                return Results.Accepted($"/api/ops/kafka/migrations/{record.Id}", record);
            return Results.Ok(record);
        }
        catch (KafkaOpsNotFoundException ex)
        {
            return Problem(ex.Message, StatusCodes.Status404NotFound);
        }
        catch (KafkaOpsRejectedException ex)
        {
            return StatusFor(ex);
        }
        catch (StaleFenceException ex)
        {
            return Conflict(ex.Message);
        }
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

public sealed class MigrationCommandBody
{
    public string? Confirmation { get; set; }
    public string? Action { get; set; }
}

public sealed class ManualStepBody
{
    public string? Workload { get; set; }
}

public sealed class BackupDeleteBody
{
    public string? Confirmation { get; set; }
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
