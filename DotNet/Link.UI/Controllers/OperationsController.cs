using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

[Route("Operations")]
public sealed class OperationsController : Controller
{
    private readonly KafkaOpsClient _kafka;

    public OperationsController(KafkaOpsClient kafka)
    {
        _kafka = kafka;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Operations";
        ViewData["OperationsSection"] = "home";
        return View();
    }

    [HttpPost("Kafka/migrations/plan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlanMigration(KafkaMigrationForm form, CancellationToken cancellationToken)
    {
        var groups = MigrationGroupList.Parse(form.AcknowledgedGroups);
        var plan = await _kafka.PlanMigrationAsync(form.Topic ?? "", form.Partitions, form.BackupSkip, form.BackupSkipAcknowledged, groups, cancellationToken);
        var page = await LoadAsync(null, null, null, null, plan.Error, MigrateQuery(form.Topic), cancellationToken);
        var drafted = WithMigrationDraft(page with
        {
            MigrationPlan = plan.Value ?? page.MigrationPlan,
            Error = KafkaPlanBanner.BesidePlan(plan.Error ?? page.Error, plan.Value?.Summary, plan.Value is not null)
        }, form);
        if (plan.Value is not null)
        {
            drafted = drafted with
            {
                HasDryRunSnapshot = true,
                DryRunPartitions = form.Partitions,
                DryRunBackupSkip = form.BackupSkip,
                DryRunBackupSkipAcknowledged = form.BackupSkipAcknowledged,
                DryRunAcknowledgedGroups = MigrationGroupList.Canonical(form.AcknowledgedGroups),
                DraftPlanHash = plan.Value.PlanHash ?? ""
            };
        }

        return View("Kafka", drafted);
    }

    [HttpPost("Kafka/migrations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestMigration(KafkaMigrationForm form, CancellationToken cancellationToken)
    {
        if (!string.Equals((form.Confirmation ?? "").Trim(), (form.Topic ?? "").Trim(), StringComparison.Ordinal))
            return await MigrateError(form, "Type the topic name to confirm the migration.", cancellationToken, keepDraft: true);

        var refusal = MigrationRequestGuard.Refusal(
            form.Partitions,
            form.BackupSkip,
            form.BackupSkipAcknowledged,
            form.AcknowledgedGroups,
            form.PlanHash,
            form.HasDryRunSnapshot,
            form.DryRunPartitions,
            form.DryRunBackupSkip,
            form.DryRunBackupSkipAcknowledged,
            form.DryRunAcknowledgedGroups);
        if (refusal is not null)
            return await MigrateError(form, refusal, cancellationToken, keepDraft: true, clearDryRun: true);

        var groups = MigrationGroupList.Parse(form.AcknowledgedGroups);
        var created = await _kafka.RequestMigrationAsync(form.Topic ?? "", form.Partitions, form.Reason ?? "", form.Confirmation ?? "", form.BackupSkip, form.BackupSkipAcknowledged, groups, form.PlanHash ?? "", cancellationToken);
        if (created.Value is null)
            return await MigrateError(form, created.Error ?? "The migration was not requested.", cancellationToken, keepDraft: true);

        return Redirect(MigrateHref(form.Topic, created.Value.Id));
    }

    [HttpPost("Kafka/migrations/approve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ApproveMigration(KafkaMigrationForm form, CancellationToken cancellationToken) =>
        MigrationPost(form, () => _kafka.ApproveMigrationAsync(form.MigrationId, cancellationToken), cancellationToken);

    [HttpPost("Kafka/migrations/execute")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ExecuteMigration(KafkaMigrationForm form, CancellationToken cancellationToken) =>
        MigrationPost(form, () => _kafka.ExecuteMigrationAsync(form.MigrationId, cancellationToken), cancellationToken);

    [HttpPost("Kafka/migrations/abort")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AbortMigration(KafkaMigrationForm form, CancellationToken cancellationToken) =>
        MigrationPost(form, () => _kafka.AbortMigrationAsync(form.MigrationId, cancellationToken), cancellationToken);

    [HttpPost("Kafka/migrations/go")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GoMigration(KafkaMigrationForm form, CancellationToken cancellationToken)
    {
        if (!string.Equals((form.Confirmation ?? "").Trim(), (form.Topic ?? "").Trim(), StringComparison.Ordinal))
            return await MigrateError(form, "Type the topic name before go.", cancellationToken);

        return await MigrationPost(form, () => _kafka.GoMigrationAsync(form.MigrationId, form.Confirmation ?? "", cancellationToken), cancellationToken);
    }

    [HttpPost("Kafka/migrations/recover")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecoverMigration(KafkaMigrationForm form, CancellationToken cancellationToken)
    {
        if (!string.Equals((form.Confirmation ?? "").Trim(), (form.Topic ?? "").Trim(), StringComparison.Ordinal))
            return await MigrateError(form, "Type the topic name to recover the original partition count.", cancellationToken);

        return await MigrationPost(form, () => _kafka.RecoverMigrationAsync(form.MigrationId, form.Confirmation ?? "", form.Action ?? "original", cancellationToken), cancellationToken);
    }

    [HttpPost("Kafka/migrations/manual")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ManualMigration(KafkaMigrationForm form, CancellationToken cancellationToken) =>
        MigrationPost(form, () => _kafka.ManualStepAsync(form.MigrationId, form.Workload ?? "", cancellationToken), cancellationToken);

    [HttpPost("Kafka/migrations/backup-delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMigrationBackup(KafkaMigrationForm form, CancellationToken cancellationToken)
    {
        if (!string.Equals((form.Confirmation ?? "").Trim(), (form.BackupName ?? "").Trim(), StringComparison.Ordinal))
            return await MigrateError(form, "Type the backup topic name to delete it.", cancellationToken);

        var deleted = await _kafka.DeleteBackupAsync(form.BackupName ?? "", form.Confirmation ?? "", cancellationToken);
        var done = await LoadAsync(null, null, null, null, deleted.Error, MigrateQuery(form.Topic), cancellationToken);
        return View("Kafka", done with { Message = deleted.Error is null ? "Backup delete was requested." : done.Message });
    }

    [HttpGet("Kafka")]
    public async Task<IActionResult> Kafka(Guid? requestId, CancellationToken cancellationToken)
    {
        var page = await LoadAsync(requestId, null, null, null, null, null, cancellationToken);
        return View(page);
    }

    [HttpGet("Kafka/requests/{id:guid}")]
    public async Task<IActionResult> RequestStatus(Guid id, CancellationToken cancellationToken)
    {
        var call = await _kafka.GetAsync(id, cancellationToken);
        if (!call.Ok)
            return Problem(detail: call.Error, statusCode: call.Status == 0 ? StatusCodes.Status502BadGateway : call.Status);
        return Json(call.Value);
    }

    [HttpPost("Kafka/plan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Plan(KafkaChangeForm form, CancellationToken cancellationToken)
    {
        var page = WithDraft(await LoadAsync(null, null, null, null, null, QueryFrom(form), cancellationToken), form);
        var plan = await _kafka.PlanAsync(form.Topic ?? "", form.Partitions, form.OverrideQuietWindow, form.OverrideReason, cancellationToken);
        return View("Kafka", page with { Plan = plan.Value, Error = KafkaPlanBanner.BesidePlan(plan.Error ?? page.Error, plan.Value?.Summary, plan.Value is not null), Query = page.Query with { View = ThroughputKafkaPageQuery.Topic, Advanced = true } });
    }

    [HttpPost("Kafka/family/plan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlanFamily(KafkaChangeForm form, CancellationToken cancellationToken)
    {
        var page = WithDraft(await LoadAsync(null, null, null, null, null, QueryFrom(form), cancellationToken), form);
        var plan = await _kafka.PlanFamilyAsync(form.Topic ?? "", form.OverrideQuietWindow, form.OverrideReason, cancellationToken);
        return View("Kafka", page with { Plan = plan.Value, Error = KafkaPlanBanner.BesidePlan(plan.Error ?? page.Error, plan.Value?.Summary, plan.Value is not null), Query = page.Query with { View = ThroughputKafkaPageQuery.Topic, Advanced = true } });
    }

    [HttpPost("Kafka/family")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFamily(KafkaChangeForm form, CancellationToken cancellationToken)
    {
        var page = WithDraft(await LoadAsync(null, null, null, null, null, QueryFrom(form), cancellationToken), form);
        page = page with { Query = page.Query with { View = ThroughputKafkaPageQuery.Topic, Advanced = true } };
        if (string.IsNullOrWhiteSpace(form.Reason))
            return View("Kafka", page with { Error = "A reason is required." });
        if (!string.Equals((form.Confirmation ?? "").Trim(), form.Topic ?? "", StringComparison.Ordinal))
            return View("Kafka", page with { Error = "Type the topic name to confirm. Adding partitions cannot be reversed." });

        var created = await _kafka.CreateFamilyAsync(
            form.Topic ?? "",
            form.Reason,
            form.OverrideQuietWindow,
            form.OverrideReason,
            form.Confirmation,
            Guid.NewGuid().ToString("N"),
            cancellationToken);
        if (created.Value is null)
            return View("Kafka", page with { Error = created.Error });
        return Redirect(page.Query.Href(topic: form.Topic) + "&requestId=" + created.Value.Id.ToString("D"));
    }

    [HttpPost("Kafka/requests")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(KafkaChangeForm form, CancellationToken cancellationToken)
    {
        var page = WithDraft(await LoadAsync(null, null, null, null, null, QueryFrom(form), cancellationToken), form);
        page = page with { Query = page.Query with { View = ThroughputKafkaPageQuery.Topic, Advanced = true } };
        if (string.IsNullOrWhiteSpace(form.Reason))
            return View("Kafka", page with { Error = "A reason is required." });
        if (!string.Equals((form.Confirmation ?? "").Trim(), form.Topic ?? "", StringComparison.Ordinal))
            return View("Kafka", page with { Error = "Type the topic name to confirm. Adding partitions cannot be reversed." });

        var created = await _kafka.CreateAsync(
            form.Topic ?? "",
            form.Partitions,
            form.Reason,
            form.OverrideQuietWindow,
            form.OverrideReason,
            form.Confirmation,
            Guid.NewGuid().ToString("N"),
            cancellationToken);
        if (created.Value is null)
            return View("Kafka", page with { Error = created.Error });
        return Redirect(page.Query.Href(topic: form.Topic) + "&requestId=" + created.Value.Id.ToString("D"));
    }

    [HttpPost("Kafka/groups/replicas/plan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlanScale(KafkaChangeForm form, CancellationToken cancellationToken)
    {
        var page = WithDraft(await LoadAsync(null, null, null, null, null, QueryFrom(form), cancellationToken), form);
        var desired = Desired(page, form);
        var plan = await _kafka.PlanScaleAsync(form.Group ?? "", desired, cancellationToken);
        return View("Kafka", page with
        {
            ScalePlan = plan.Value,
            Error = KafkaPlanBanner.BesidePlan(plan.Error ?? page.Error, plan.Value?.Summary, plan.Value is not null),
            DraftReplicas = desired,
            Query = page.Query with { View = ThroughputKafkaPageQuery.Consumers }
        });
    }

    [HttpPost("Kafka/groups/replicas")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateScale(KafkaChangeForm form, CancellationToken cancellationToken)
    {
        var page = WithDraft(await LoadAsync(null, null, null, null, null, QueryFrom(form), cancellationToken), form);
        page = page with { Query = page.Query with { View = ThroughputKafkaPageQuery.Consumers } };
        if (string.IsNullOrWhiteSpace(form.Reason))
            return View("Kafka", page with { Error = "A reason is required." });
        var desired = Desired(page, form);
        var created = await _kafka.CreateScaleAsync(form.Group ?? "", desired, form.Reason, Guid.NewGuid().ToString("N"), cancellationToken);
        if (created.Value is null)
            return View("Kafka", page with { Error = created.Error, DraftReplicas = desired });
        return Redirect(page.Query.Href() + "&requestId=" + created.Value.Id.ToString("D"));
    }

    [HttpPost("Kafka/brokers/decommission/plan")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> PlanDecommission(KafkaChangeForm form, CancellationToken cancellationToken) =>
        BrokerPlan(form, (id, token) => _kafka.PlanDecommissionAsync(id, token), cancellationToken);

    [HttpPost("Kafka/brokers/decommission")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreateDecommission(KafkaChangeForm form, CancellationToken cancellationToken) =>
        BrokerCreate(form, (id, reason, correlation, token) => _kafka.CreateDecommissionAsync(id, reason, correlation, token), cancellationToken);

    [HttpPost("Kafka/brokers/rebalance/plan")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> PlanRebalance(KafkaChangeForm form, CancellationToken cancellationToken) =>
        BrokerPlan(form, (id, token) => _kafka.PlanRebalanceAsync(id, token), cancellationToken);

    [HttpPost("Kafka/brokers/rebalance")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreateRebalance(KafkaChangeForm form, CancellationToken cancellationToken) =>
        BrokerCreate(form, (id, reason, correlation, token) => _kafka.CreateRebalanceAsync(id, reason, correlation, token), cancellationToken);

    [HttpPost("Kafka/brokers")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBroker(KafkaChangeForm form, CancellationToken cancellationToken)
    {
        var page = WithDraft(await LoadAsync(null, null, null, null, null, QueryFrom(form), cancellationToken), form);
        page = page with { Query = page.Query with { View = ThroughputKafkaPageQuery.Brokers } };
        if (string.IsNullOrWhiteSpace(form.AddBrokerReason))
            return View("Kafka", page with { Error = "A reason is required." });
        var created = await _kafka.CreateAddBrokerAsync(form.AddBrokerReason, Guid.NewGuid().ToString("N"), cancellationToken);
        if (created.Value is null)
            return View("Kafka", page with { Error = created.Error });
        return Redirect(page.Query.Href() + "&requestId=" + created.Value.Id.ToString("D"));
    }

    [HttpPost("Kafka/requests/{id:guid}/approve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(Guid id, KafkaChangeForm form, CancellationToken cancellationToken) =>
        Mutate(id, form, () => _kafka.ApproveAsync(id, cancellationToken), cancellationToken);

    [HttpPost("Kafka/requests/{id:guid}/reject")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(Guid id, KafkaChangeForm form, CancellationToken cancellationToken) =>
        Mutate(id, form, () => _kafka.RejectAsync(id, form.Reason ?? "", cancellationToken), cancellationToken);

    [HttpPost("Kafka/requests/{id:guid}/execute")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Execute(Guid id, KafkaChangeForm form, CancellationToken cancellationToken) =>
        Mutate(id, form, () => _kafka.ExecuteAsync(id, cancellationToken), cancellationToken);

    [HttpPost("Kafka/requests/{id:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, KafkaChangeForm form, CancellationToken cancellationToken)
    {
        var call = await _kafka.CancelAsync(id, cancellationToken);
        var query = QueryFrom(form);
        if (call.Value is null)
        {
            var page = await LoadAsync(id, null, null, null, call.Error, query, cancellationToken);
            return View("Kafka", page);
        }

        return Redirect(query.Href() + "&requestId=" + call.Value.Id.ToString("D"));
    }

    private async Task<IActionResult> BrokerPlan(KafkaChangeForm form, Func<int, CancellationToken, Task<KafkaOpsCall<BrokerMovePlan>>> action, CancellationToken cancellationToken)
    {
        var page = WithDraft(await LoadAsync(null, null, null, null, null, QueryFrom(form), cancellationToken), form);
        var brokerId = BrokerId(page, form);
        var plan = await action(brokerId, cancellationToken);
        return View("Kafka", page with
        {
            MovePlan = plan.Value,
            Error = KafkaPlanBanner.BesidePlan(plan.Error ?? page.Error, plan.Value?.Summary, plan.Value is not null),
            DraftBrokerId = brokerId,
            Query = page.Query with { View = ThroughputKafkaPageQuery.Brokers, Broker = brokerId.ToString() }
        });
    }

    private async Task<IActionResult> BrokerCreate(KafkaChangeForm form, Func<int, string, string, CancellationToken, Task<KafkaOpsCall<ChangeRequestRecord>>> action, CancellationToken cancellationToken)
    {
        var page = WithDraft(await LoadAsync(null, null, null, null, null, QueryFrom(form), cancellationToken), form);
        page = page with { Query = page.Query with { View = ThroughputKafkaPageQuery.Brokers } };
        if (string.IsNullOrWhiteSpace(form.Reason))
            return View("Kafka", page with { Error = "A reason is required." });
        var brokerId = BrokerId(page, form);
        var created = await action(brokerId, form.Reason, Guid.NewGuid().ToString("N"), cancellationToken);
        if (created.Value is null)
            return View("Kafka", page with { Error = created.Error, DraftBrokerId = brokerId });
        return Redirect(page.Query.Href(broker: brokerId.ToString()) + "&requestId=" + created.Value.Id.ToString("D"));
    }

    private async Task<IActionResult> Mutate(Guid id, KafkaChangeForm form, Func<Task<KafkaOpsCall<ChangeRequestRecord>>> action, CancellationToken cancellationToken)
    {
        var call = await action();
        var query = QueryFrom(form);
        if (call.Value is null)
        {
            var page = await LoadAsync(id, null, null, null, call.Error, query, cancellationToken);
            return View("Kafka", page);
        }

        return Redirect(query.Href() + "&requestId=" + id.ToString("D"));
    }

    private async Task<ThroughputKafkaPage> LoadAsync(Guid? requestId, PartitionPlan? plan, ReplicaScalePlan? scalePlan, BrokerMovePlan? movePlan, string? error, ThroughputKafkaPageQuery? queryOverride, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Kafka";
        ViewData["OperationsSection"] = "kafka";
        var query = queryOverride ?? ThroughputKafkaPageQuery.From(Request.Query, ReturnUrlRules.FromQuery(Request));
        var topicsCall = await _kafka.GetTopicsAsync(cancellationToken);
        var groupsCall = await _kafka.GetGroupsAsync(query.Tests, cancellationToken);
        var capabilitiesCall = await _kafka.GetCapabilitiesAsync(cancellationToken);
        var clusterCall = await _kafka.GetClusterAsync(cancellationToken);
        var infraCall = await _kafka.GetInfraAsync(cancellationToken);
        var topics = topicsCall.Value ?? new KafkaTopicsResponse { Error = topicsCall.Error };
        var groups = groupsCall.Value ?? new KafkaGroupsResponse { Error = groupsCall.Error };
        var capabilities = capabilitiesCall.Value ?? new KafkaCapabilitiesResponse { Error = capabilitiesCall.Error };
        var cluster = clusterCall.Value ?? new ClusterSnapshot { Error = clusterCall.Error };
        var infra = infraCall.Value ?? new InfraStatus { Detail = infraCall.Error ?? KafkaOpsFixture.DisabledDetail };
        var families = topics.Topics.Select(row => row.Family).Where(family => family.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var keyClasses = topics.Topics.Select(row => row.KeyClass).Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var topicRows = SortTopics(FilterTopics(topics.Topics, query).ToList(), query);
        var topicCount = topicRows.Count;
        var topicPages = Math.Max(1, (int)Math.Ceiling(topicCount / (double)query.PageSize));
        var chartTopics = topicRows.Take(12).ToList();
        var topicPage = Math.Min(query.Page, topicPages);
        if (query.View == ThroughputKafkaPageQuery.Overview)
            query = query with { Page = topicPage };
        var pagedTopics = topicRows.Skip((topicPage - 1) * query.PageSize).Take(query.PageSize).ToList();

        var groupRows = SortGroups(FilterGroups(groups.Groups, query).ToList(), query);
        var groupCount = groupRows.Count;
        var groupPages = Math.Max(1, (int)Math.Ceiling(groupCount / (double)query.PageSize));
        var groupPage = Math.Min(query.Page, groupPages);
        if (query.View == ThroughputKafkaPageQuery.Consumers)
            query = query with { Page = groupPage };
        var pagedGroups = groupRows.Skip((groupPage - 1) * query.PageSize).Take(query.PageSize).ToList();

        var brokerRows = SortBrokers(FilterBrokers(cluster.Brokers, query).ToList(), query);
        var brokerCount = brokerRows.Count;
        var brokerPages = Math.Max(1, (int)Math.Ceiling(brokerCount / (double)query.PageSize));
        var brokerPage = Math.Min(query.Page, brokerPages);
        if (query.View == ThroughputKafkaPageQuery.Brokers)
            query = query with { Page = brokerPage };
        var pagedBrokers = brokerRows.Skip((brokerPage - 1) * query.PageSize).Take(query.PageSize).ToList();

        var selectedGroup = groups.Groups.FirstOrDefault(row => string.Equals(row.GroupId, query.Group, StringComparison.Ordinal));
        var selectedTopic = topics.Topics.FirstOrDefault(row => string.Equals(row.Topic, query.TopicName, StringComparison.Ordinal));
        var selectedBroker = int.TryParse(query.Broker, out var brokerId)
            ? cluster.Brokers.FirstOrDefault(row => row.Id == brokerId)
            : null;
        var partitions = BuildPartitions(selectedTopic, cluster, groups.Groups);
        var ceiling = ReplicaCeiling(selectedGroup, topics.Topics);
        KafkaTopicDetail? detail = null;
        KafkaTopicConfigs? configs = null;
        KafkaMigrationRecord? migration = null;
        string? runbook = null;
        if (query.View == ThroughputKafkaPageQuery.Migrate && !string.IsNullOrWhiteSpace(query.TopicName))
        {
            var detailCall = await _kafka.GetDetailAsync(query.TopicName, cancellationToken);
            detail = detailCall.Value;
            error ??= detailCall.Error;
            var diff = Request.Query["diff"].ToString();
            var configCall = await _kafka.GetConfigsAsync(query.TopicName, diff, cancellationToken);
            configs = configCall.Value;
            error ??= configCall.Error;
        }

        if (Guid.TryParse(Request.Query["migration"], out var migrationId) && migrationId != Guid.Empty)
        {
            var loadedMigration = await _kafka.GetMigrationAsync(migrationId, cancellationToken);
            migration = loadedMigration.Value;
            error ??= loadedMigration.Error;
            if (migration is not null)
            {
                var book = await _kafka.RunbookAsync(migration.Id, cancellationToken);
                runbook = book.Value;
            }
        }

        ChangeRequestRecord? request = null;
        if (requestId is Guid id && id != Guid.Empty)
        {
            var loaded = await _kafka.GetAsync(id, cancellationToken);
            request = loaded.Value;
            error ??= loaded.Error;
        }

        error ??= topics.Error ?? groups.Error ?? cluster.Error ?? capabilities.Error;
        if (string.IsNullOrWhiteSpace(error))
            error = topics.GroupsError;
        return new ThroughputKafkaPage
        {
            Query = query,
            Topics = topics,
            Groups = groups,
            Capabilities = capabilities,
            Cluster = cluster,
            Infra = infra,
            TopicRows = pagedTopics,
            GroupRows = pagedGroups,
            BrokerRows = pagedBrokers,
            ChartTopics = chartTopics,
            TopicCount = topicCount,
            GroupCount = groupCount,
            BrokerCount = brokerCount,
            TopicPages = topicPages,
            GroupPages = groupPages,
            BrokerPages = brokerPages,
            Families = families,
            KeyClasses = keyClasses,
            Plan = plan,
            ScalePlan = scalePlan,
            MovePlan = movePlan,
            Request = request,
            Error = error,
            SelectedGroup = selectedGroup,
            SelectedTopic = selectedTopic,
            SelectedBroker = selectedBroker,
            Partitions = partitions,
            ReplicaCeiling = ceiling,
            Detail = detail,
            Configs = configs,
            Migration = migration,
            Runbook = runbook
        };
    }

    private static List<KafkaPartitionDetail> BuildPartitions(KafkaTopicRow? topic, ClusterSnapshot cluster, IReadOnlyList<KafkaGroupRow> groups)
    {
        if (topic is null)
            return [];

        return cluster.Placements
            .Where(row => string.Equals(row.Topic, topic.Topic, StringComparison.Ordinal))
            .OrderBy(row => row.Partition)
            .Select(row =>
            {
                var lag = new List<KafkaPartitionLagRow>();
                foreach (var group in groups)
                {
                    foreach (var item in group.Partitions)
                    {
                        if (!string.Equals(item.Topic, row.Topic, StringComparison.Ordinal) || item.Partition != row.Partition)
                            continue;
                        lag.Add(new KafkaPartitionLagRow
                        {
                            GroupId = group.GroupId,
                            Topic = item.Topic,
                            Partition = item.Partition,
                            HighWatermark = item.HighWatermark,
                            Committed = item.Committed,
                            Lag = item.Lag,
                            Owned = item.Owned
                        });
                    }
                }
                return new KafkaPartitionDetail
                {
                    Partition = row.Partition,
                    Leader = row.Leader,
                    Replicas = string.Join(", ", row.Replicas),
                    InSync = string.Join(", ", row.Isr),
                    HighWatermark = lag.Count == 0 ? -1 : lag.Max(item => item.HighWatermark),
                    Lag = lag
                };
            })
            .ToList();
    }

    private static int ReplicaCeiling(KafkaGroupRow? group, IReadOnlyList<KafkaTopicRow> topics)
    {
        if (group is null)
            return 0;
        var subscribed = topics.Where(row => row.Groups.Contains(group.GroupId, StringComparer.Ordinal)).ToList();
        if (subscribed.Count == 0)
            return 0;
        return subscribed.Min(row => row.MaxReplicas > 0 ? row.MaxReplicas : row.Partitions);
    }

    private static IEnumerable<KafkaTopicRow> FilterTopics(IEnumerable<KafkaTopicRow> rows, ThroughputKafkaPageQuery query)
    {
        foreach (var row in rows)
        {
            if (query.Family.Length > 0 && !string.Equals(row.Family, query.Family, StringComparison.OrdinalIgnoreCase))
                continue;
            if (query.KeyClass.Length > 0 && !string.Equals(row.KeyClass, query.KeyClass, StringComparison.OrdinalIgnoreCase))
                continue;
            if (query.Q.Length > 0
                && !row.Topic.Contains(query.Q, StringComparison.OrdinalIgnoreCase)
                && !row.KeyClass.Contains(query.Q, StringComparison.OrdinalIgnoreCase)
                && !row.Groups.Any(group => group.Contains(query.Q, StringComparison.OrdinalIgnoreCase)))
                continue;
            yield return row;
        }
    }

    private static IEnumerable<KafkaGroupRow> FilterGroups(IEnumerable<KafkaGroupRow> rows, ThroughputKafkaPageQuery query)
    {
        foreach (var row in rows)
        {
            if (query.Q.Length > 0
                && !row.GroupId.Contains(query.Q, StringComparison.OrdinalIgnoreCase)
                && !row.State.Contains(query.Q, StringComparison.OrdinalIgnoreCase)
                && !row.Members.Any(member => member.ClientId.Contains(query.Q, StringComparison.OrdinalIgnoreCase) || member.Host.Contains(query.Q, StringComparison.OrdinalIgnoreCase)))
                continue;
            yield return row;
        }
    }

    private static IEnumerable<BrokerSnapshot> FilterBrokers(IEnumerable<BrokerSnapshot> rows, ThroughputKafkaPageQuery query)
    {
        foreach (var row in rows)
        {
            if (query.Q.Length > 0
                && !row.Host.Contains(query.Q, StringComparison.OrdinalIgnoreCase)
                && !row.Id.ToString().Contains(query.Q, StringComparison.Ordinal)
                && !row.Rack.Contains(query.Q, StringComparison.OrdinalIgnoreCase)
                && !row.State.Contains(query.Q, StringComparison.OrdinalIgnoreCase))
                continue;
            yield return row;
        }
    }

    private static List<KafkaTopicRow> SortTopics(List<KafkaTopicRow> rows, ThroughputKafkaPageQuery query)
    {
        IEnumerable<KafkaTopicRow> ordered = query.Sort switch
        {
            "partitions" => rows.OrderBy(row => row.Partitions),
            "lag" => rows.OrderBy(row => row.TotalLag),
            "rate" => rows.OrderBy(row => row.ProduceRatePerSecond),
            "key" => rows.OrderBy(row => row.KeyClass, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderBy(row => row.Topic, StringComparer.OrdinalIgnoreCase)
        };
        if (query.Dir == "desc")
            ordered = ordered.Reverse();
        return ordered.ToList();
    }

    private static List<KafkaGroupRow> SortGroups(List<KafkaGroupRow> rows, ThroughputKafkaPageQuery query)
    {
        IEnumerable<KafkaGroupRow> ordered = query.Sort switch
        {
            "state" => rows.OrderBy(row => row.State, StringComparer.OrdinalIgnoreCase),
            "lag" => rows.OrderBy(row => row.TotalLag),
            _ => rows.OrderBy(row => row.GroupId, StringComparer.OrdinalIgnoreCase)
        };
        if (query.Dir == "desc")
            ordered = ordered.Reverse();
        return ordered.ToList();
    }

    private static List<BrokerSnapshot> SortBrokers(List<BrokerSnapshot> rows, ThroughputKafkaPageQuery query)
    {
        IEnumerable<BrokerSnapshot> ordered = query.Sort switch
        {
            "partitions" or "brokers" => rows.OrderBy(row => row.PartitionCount),
            "leaders" => rows.OrderBy(row => row.LeaderCount),
            _ => rows.OrderBy(row => row.Id)
        };
        if (query.Dir == "desc")
            ordered = ordered.Reverse();
        return ordered.ToList();
    }

    private async Task<IActionResult> MigrateError(KafkaMigrationForm form, string error, CancellationToken cancellationToken, bool keepDraft = false, bool clearDryRun = false)
    {
        var page = await LoadAsync(null, null, null, null, error, MigrateQuery(form.Topic), cancellationToken);
        var migration = page.Migration;
        if (migration is null && form.MigrationId != Guid.Empty)
            migration = (await _kafka.GetMigrationAsync(form.MigrationId, cancellationToken)).Value;
        var drafted = page with { Migration = migration, Error = error };
        if (keepDraft)
            drafted = WithMigrationDraft(drafted, form);
        if (clearDryRun)
            drafted = drafted with { HasDryRunSnapshot = false, DraftPlanHash = "" };
        return View("Kafka", drafted);
    }

    private async Task<IActionResult> MigrationPost(KafkaMigrationForm form, Func<Task<KafkaOpsCall<KafkaMigrationRecord>>> call, CancellationToken cancellationToken)
    {
        var result = await call();
        if (result.Value is not null)
            return Redirect(MigrateHref(result.Value.Topic, result.Value.Id));

        var page = await LoadAsync(null, null, null, null, result.Error, MigrateQuery(form.Topic), cancellationToken);
        var migration = page.Migration;
        if (migration is null && form.MigrationId != Guid.Empty)
            migration = (await _kafka.GetMigrationAsync(form.MigrationId, cancellationToken)).Value;
        return View("Kafka", page with { Migration = migration, Error = result.Error ?? page.Error });
    }

    private static ThroughputKafkaPageQuery MigrateQuery(string? topic) =>
        new() { View = ThroughputKafkaPageQuery.Migrate, TopicName = topic ?? "" };

    private static string MigrateHref(string? topic, Guid id) =>
        "/Operations/Kafka?view=migrate&topic=" + Uri.EscapeDataString(topic ?? "") + "&migration=" + id.ToString("D");

    private static ThroughputKafkaPage WithMigrationDraft(ThroughputKafkaPage page, KafkaMigrationForm form)
    {
        var snapshot = form.HasDryRunSnapshot;
        return page with
        {
            DraftPartitions = form.Partitions,
            DraftReason = form.Reason ?? "",
            DraftConfirmation = form.Confirmation ?? "",
            DraftBackupSkip = form.BackupSkip,
            DraftBackupSkipAcknowledged = form.BackupSkipAcknowledged,
            DraftAcknowledgedGroups = form.AcknowledgedGroups ?? "",
            DraftPlanHash = snapshot ? form.PlanHash ?? "" : page.DraftPlanHash,
            HasDryRunSnapshot = snapshot,
            DryRunPartitions = form.DryRunPartitions,
            DryRunBackupSkip = form.DryRunBackupSkip,
            DryRunBackupSkipAcknowledged = form.DryRunBackupSkipAcknowledged,
            DryRunAcknowledgedGroups = form.DryRunAcknowledgedGroups ?? ""
        };
    }

    private static ThroughputKafkaPage WithDraft(ThroughputKafkaPage page, KafkaChangeForm form) =>
        page with
        {
            DraftTopic = form.Topic ?? page.Query.TopicName,
            DraftPartitions = form.Partitions,
            DraftReason = form.Reason ?? page.DraftReason,
            DraftAddBrokerReason = form.AddBrokerReason ?? page.DraftAddBrokerReason,
            DraftConfirmation = form.Confirmation ?? "",
            DraftOverride = form.OverrideQuietWindow,
            DraftOverrideReason = form.OverrideReason ?? "",
            DraftReplicas = form.Replicas,
            DraftDelta = form.Delta < 1 ? 1 : form.Delta,
            DraftBrokerId = form.BrokerId
        };

    private static int Desired(ThroughputKafkaPage page, KafkaChangeForm form)
    {
        var current = page.SelectedGroup?.Members.Count ?? 0;
        var delta = form.Delta < 1 ? 1 : form.Delta;
        return form.Adjust switch
        {
            "add" => current + delta,
            "remove" => Math.Max(0, current - delta),
            _ => form.Replicas
        };
    }

    private static int BrokerId(ThroughputKafkaPage page, KafkaChangeForm form) =>
        form.BrokerId >= 0 ? form.BrokerId : page.SelectedBroker?.Id ?? -1;

    private static ThroughputKafkaPageQuery QueryFrom(KafkaChangeForm form)
    {
        var view = form.View;
        if (view is not (ThroughputKafkaPageQuery.Overview or ThroughputKafkaPageQuery.Topic or ThroughputKafkaPageQuery.Consumers or ThroughputKafkaPageQuery.Brokers))
            view = ThroughputKafkaPageQuery.Overview;
        return new ThroughputKafkaPageQuery
        {
            Q = form.Q ?? "",
            Sort = string.IsNullOrWhiteSpace(form.Sort) ? "topic" : form.Sort,
            Dir = form.Dir == "desc" ? "desc" : "asc",
            Page = form.Page < 1 ? 1 : form.Page,
            PageSize = form.PageSize is 10 or 25 or 50 ? form.PageSize : 25,
            Family = form.Family ?? "",
            KeyClass = form.KeyClass ?? "",
            View = view,
            Group = form.Group ?? "",
            TopicName = form.Topic ?? "",
            Broker = form.Broker ?? "",
            Tests = form.Tests,
            Advanced = form.Advanced,
            ReturnUrl = ReturnUrlRules.Sanitize(form.ReturnUrl)
        };
    }
}

public sealed class KafkaChangeForm
{
    public string? Topic { get; set; }
    public int Partitions { get; set; }
    public string? Reason { get; set; }
    public string? AddBrokerReason { get; set; }
    public string? Confirmation { get; set; }
    public bool OverrideQuietWindow { get; set; }
    public string? OverrideReason { get; set; }
    public string? Q { get; set; }
    public string? Sort { get; set; }
    public string? Dir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Family { get; set; }
    public string? KeyClass { get; set; }
    public string? View { get; set; }
    public string? Group { get; set; }
    public string? Broker { get; set; }
    public int BrokerId { get; set; } = -1;
    public int Replicas { get; set; }
    public int Delta { get; set; } = 1;
    public string? Adjust { get; set; }
    public bool Tests { get; set; }
    public bool Advanced { get; set; }
    public string? ReturnUrl { get; set; }
}
