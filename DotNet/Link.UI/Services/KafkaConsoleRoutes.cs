using Link.UI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Link.UI.Services;

public static class KafkaConsoleRoutes
{
    public const string Summary = "Summary";
    public const string Messages = "Messages";
    public const string Produce = "Produce";
    public const string Partitions = "Partitions";
    public const string Replication = "Replication";
    public const string Consumers = "Consumers";

    public static readonly string[] Subtabs = [Summary, Messages, Produce, Partitions, Replication, Consumers];

    public static string? CanonicalSubtab(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Summary;
        foreach (var tab in Subtabs)
        {
            if (string.Equals(tab, raw.Trim(), StringComparison.OrdinalIgnoreCase))
                return tab;
        }

        return null;
    }

    public static string Overview() => "/Operations/Kafka";

    public static string Topics(string? topic, string? subtab)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return "/Operations/Kafka/Topics";
        var tab = CanonicalSubtab(subtab) ?? Summary;
        return "/Operations/Kafka/Topics/" + Esc(topic.Trim()) + "/" + tab;
    }

    public static string Group(string? group)
    {
        if (string.IsNullOrWhiteSpace(group))
            return "/Operations/Kafka/ConsumerGroups";
        return "/Operations/Kafka/ConsumerGroups/" + Esc(group.Trim());
    }

    public static string Broker(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "/Operations/Kafka/Brokers";
        return "/Operations/Kafka/Brokers/" + Esc(id.Trim());
    }

    public static string PathFor(string view, string? topic, string? group, string? broker, string? subtab)
    {
        if (view == ThroughputKafkaPageQuery.Consumers)
            return Group(group);
        if (view == ThroughputKafkaPageQuery.Brokers)
            return Broker(broker);
        if (view == ThroughputKafkaPageQuery.Overview)
            return Overview();

        var tab = view switch
        {
            ThroughputKafkaPageQuery.Messages => Messages,
            ThroughputKafkaPageQuery.Produce => Produce,
            ThroughputKafkaPageQuery.Migrate => Partitions,
            _ => CanonicalSubtab(subtab) ?? Summary
        };
        return Topics(topic, tab);
    }

    public static string? LegacyRedirect(IQueryCollection query)
    {
        if (!query.ContainsKey("view"))
            return null;

        var parsed = ThroughputKafkaPageQuery.From(query, null);
        var path = PathFor(parsed.View, parsed.TopicName, parsed.Group, parsed.Broker, null);
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "view" };
        if (parsed.View is ThroughputKafkaPageQuery.Topic or ThroughputKafkaPageQuery.Messages or ThroughputKafkaPageQuery.Produce or ThroughputKafkaPageQuery.Migrate)
            skip.Add("topic");
        else if (parsed.View == ThroughputKafkaPageQuery.Consumers)
            skip.Add("group");
        else if (parsed.View == ThroughputKafkaPageQuery.Brokers)
            skip.Add("broker");

        var pairs = new List<string>();
        foreach (var key in query.Keys)
        {
            if (skip.Contains(key) || string.IsNullOrWhiteSpace(key))
                continue;
            var value = query[key].ToString();
            if (string.IsNullOrWhiteSpace(value))
                continue;
            pairs.Add(Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value));
        }

        return pairs.Count == 0 ? path : path + "?" + string.Join("&", pairs);
    }

    public static string With(string href, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            return href;
        var separator = href.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return href + separator + Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value);
    }

    public static ThroughputKafkaPageQuery Bind(IQueryCollection query, string? returnUrl, string view, string? topic, string? group, string? broker, string? subtab)
    {
        var parsed = ThroughputKafkaPageQuery.From(query, returnUrl);
        return parsed with
        {
            View = view,
            TopicName = topic ?? "",
            Group = group ?? "",
            Broker = broker ?? "",
            Subtab = subtab ?? ""
        };
    }

    private static string Esc(string value) => Uri.EscapeDataString(value);
}
