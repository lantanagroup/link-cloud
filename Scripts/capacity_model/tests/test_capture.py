"""Tests for the capture collectors and snapshot.py, against fakes and recorded responses. Run from the repository
root:

    python -m unittest discover Scripts/capacity_model/tests

No database, broker or client library is needed: confluent-kafka is stubbed when it isn't installed.
"""

import argparse
import json
import sys
import tempfile
import types
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
FIXTURES = HERE / "fixtures"

try:  # stub confluent_kafka's value types when the library isn't installed
    import confluent_kafka  # noqa: F401
except ImportError:
    stub = types.ModuleType("confluent_kafka")

    class TopicPartition:
        def __init__(self, topic, partition, offset=-1001):
            self.topic, self.partition, self.offset = topic, partition, offset

    class ConsumerGroupTopicPartitions:
        def __init__(self, group_id, topic_partitions=None):
            self.group_id, self.topic_partitions = group_id, topic_partitions or []

    stub.TopicPartition = TopicPartition
    stub.ConsumerGroupTopicPartitions = ConsumerGroupTopicPartitions
    sys.modules["confluent_kafka"] = stub

from confluent_kafka import ConsumerGroupTopicPartitions, TopicPartition  # noqa: E402

import snapshot  # noqa: E402
from capture import cloud, kafka, kafka_rest, loki, mongo, sql  # noqa: E402
from capture import targets as targets_mod  # noqa: E402
from capture.common import Unavailable, cadence_of, normalize_guid  # noqa: E402

R1 = "11111111-1111-1111-1111-111111111111"
R2 = "22222222-2222-2222-2222-222222222222"


# --------------------------------------------------------------------------------------------
# Fakes
# --------------------------------------------------------------------------------------------

class FakeCursor:
    def __init__(self, conn):
        self.conn = conn
        self.result = []

    def execute(self, query, params=()):
        self.conn.executed.append((query, params))
        for marker, rows in self.conn.handlers:
            if marker in query:
                self.result = rows(query, params) if callable(rows) else rows
                return
        raise AssertionError(f"unexpected query: {query[:120]}")

    def fetchall(self):
        return list(self.result)

    def close(self):
        pass


class FakeConn:
    def __init__(self, handlers):
        self.handlers = handlers
        self.executed = []

    def cursor(self):
        return FakeCursor(self)

    def close(self):
        pass


class FakeCollection:
    def __init__(self, docs, count=None):
        self.docs, self.count, self.queries = docs, count, []

    def estimated_document_count(self):
        return self.count if self.count is not None else len(self.docs)

    def find(self, query, projection):
        self.queries.append((query, projection))
        return iter(self.docs)


class FakeMongoDb:
    def __init__(self, collections, stats=None):
        self.collections, self.stats = collections, stats

    def list_collection_names(self):
        return list(self.collections)

    def __getitem__(self, name):
        return self.collections[name]

    def command(self, *args):
        if self.stats is None:
            raise RuntimeError("command not supported")
        return self.stats


class FakeMessage:
    def __init__(self, partition, offset, key=b"k", value=b"vvvv", headers=None):
        self._p, self._o, self._k, self._v, self._h = partition, offset, key, value, headers

    def error(self):
        return None

    def partition(self):
        return self._p

    def offset(self):
        return self._o

    def key(self):
        return self._k

    def value(self):
        return self._v

    def headers(self):
        return self._h


class FakeConsumer:
    def __init__(self, marks, messages=None):
        self.marks, self.messages, self.assigned = marks, messages or [], None

    def get_watermark_offsets(self, tp, timeout=None, cached=False):
        return self.marks[(tp.topic, tp.partition)]

    def assign(self, tps):
        self.assigned = [(tp.topic, tp.partition, tp.offset) for tp in tps]

    def consume(self, num_messages, timeout):
        batch, self.messages = self.messages, []
        return batch

    def unassign(self):
        pass

    def close(self):
        pass


class Future:
    def __init__(self, value):
        self.value = value

    def result(self):
        return self.value


class FakeAdmin:
    def __init__(self, topics, groups=None):
        self.topics, self.groups = topics, groups or {}

    def list_topics(self, timeout=None):
        md = types.SimpleNamespace(topics={})
        for name, parts in self.topics.items():
            md.topics[name] = types.SimpleNamespace(
                error=None, partitions={p: types.SimpleNamespace(replicas=[1, 2, 3]) for p in range(parts)})
        return md

    def list_consumer_groups(self, request_timeout=None):
        return Future(types.SimpleNamespace(valid=[types.SimpleNamespace(group_id=g) for g in self.groups]))

    def list_consumer_group_offsets(self, requests, request_timeout=None):
        gid = requests[0].group_id
        tps = [TopicPartition(t, p, o) for (t, p), o in self.groups[gid].items()]
        return {gid: Future(ConsumerGroupTopicPartitions(gid, tps))}


# --------------------------------------------------------------------------------------------
# Common helpers
# --------------------------------------------------------------------------------------------

class CommonTests(unittest.TestCase):
    def test_cadence_mapping(self):
        self.assertEqual(cadence_of("Daily"), "daily")
        self.assertEqual(cadence_of("Monthly"), "monthly")
        self.assertEqual(cadence_of("Adhoc"), "other")
        self.assertEqual(cadence_of(None), "other")

    def test_guid_normalisation(self):
        self.assertEqual(normalize_guid("{" + R1.upper() + "}"), R1)
        with self.assertRaises(ValueError):
            normalize_guid("1; drop table x")


# --------------------------------------------------------------------------------------------
# SQL
# --------------------------------------------------------------------------------------------

class SqlTests(unittest.TestCase):
    def test_table_sizes_and_multi_table_store(self):
        conn = FakeConn([("dm_db_partition_stats", [("dbo", "ReportEntry", 10, 81920, 16384),
                                                     ("quartz", "QRTZ_TRIGGERS", 2, 8192, 0),
                                                     ("quartz", "QRTZ_JOB_DETAILS", 3, 8192, 8192)])])
        sizes = sql.table_sizes(conn)
        self.assertEqual(sizes["dbo.reportentry"], {"rows": 10, "data_bytes": 81920, "index_bytes": 16384})
        both = sql.store_size(sizes, {"tables": ["quartz.QRTZ_TRIGGERS", "quartz.QRTZ_JOB_DETAILS"]})
        self.assertEqual(both, {"rows": 5, "data_bytes": 16384, "index_bytes": 8192})
        self.assertIsNone(sql.store_size(sizes, {"table": "dbo.Missing"}))

    def test_attribution_uses_join_and_chunks_report_ids(self):
        ids = [normalize_guid(f"{i:032x}") for i in range(1, 1201)]
        conn = FakeConn([("COUNT_BIG", lambda q, p: [(R1.upper(), 3)] if R1 in q else [])])
        loc = {"table": "dbo.FhirQuery", "attribution": {
            "key": "l.ReportTrackingId", "from": "dbo.FhirQuery t JOIN dbo.DataAcquisitionLog l ON l.Id = t.DataAcquisitionLogId"}}
        counts = sql.attribution_counts(conn, loc, ids + [R1])
        self.assertEqual(counts, {R1: 3})
        self.assertEqual(len(conn.executed), 3)  # 1,201 ids in chunks of 500
        self.assertIn("JOIN dbo.DataAcquisitionLog l", conn.executed[0][0])
        self.assertIn("GROUP BY l.ReportTrackingId", conn.executed[0][0])

    def test_phase_counts_group_by_the_phase_column(self):
        conn = FakeConn([("COUNT_BIG", [("Initial", 100), ("Supplemental", 60)])])
        loc = {"table": "dbo.FhirQuery", "attribution": {
            "key": "l.ReportTrackingId", "phase": "l.QueryPhase",
            "from": "dbo.FhirQuery t JOIN dbo.DataAcquisitionLog l ON l.Id = t.DataAcquisitionLogId"}}
        self.assertEqual(sql.phase_counts(conn, loc, [R1]), {"Initial": 100, "Supplemental": 60})
        self.assertIn("GROUP BY l.QueryPhase", conn.executed[0][0])

    def test_attribution_rejects_non_guid_ids(self):
        conn = FakeConn([("COUNT_BIG", [])])
        with self.assertRaises(ValueError):
            sql.attribution_counts(conn, {"table": "dbo.X", "attribution": {"key": "Id"}}, ["x' OR 1=1 --"])

    def test_run_reports_since_computes_cadence_and_lag(self):
        end = datetime(2026, 11, 1)
        conn = FakeConn([("FROM dbo.ReportSchedule", [
            (R1.upper(), "fac-a", "Monthly", datetime(2026, 10, 1), end, "Submitted", datetime(2026, 11, 1, 2),
             end + timedelta(hours=36)),
            (R2, "fac-a", "Daily", datetime(2026, 10, 31), end, "Pending", datetime(2026, 11, 1, 2), None)])])
        since = datetime(2026, 11, 1, 1, tzinfo=timezone(timedelta(hours=-5)))
        reports = sql.run_reports(conn, since=since)
        self.assertEqual(conn.executed[0][1], (datetime(2026, 11, 1, 6),))  # naive UTC
        self.assertIn("SWITCHOFFSET", conn.executed[0][0])
        self.assertEqual([r["cadence"] for r in reports], ["monthly", "daily"])
        self.assertAlmostEqual(reports[0]["completion_lag_days"], 1.5)
        self.assertIsNone(reports[1]["completion_lag_days"])
        self.assertEqual(reports[0]["id"], R1)

    def test_census_and_facilities(self):
        conn = FakeConn([("PatientEncounters", [(12, 9)]), ("Facilities", [(3,)])])
        self.assertEqual(sql.census_patients(conn, datetime(2026, 11, 1, tzinfo=timezone.utc)),
                         {"encounters": 12, "patients": 9})
        self.assertEqual(sql.facility_count(conn), 3)

    def test_wait_stats_view_depends_on_engine(self):
        rows = [("LCK_M_X", 4, 900, 10)]
        conn = FakeConn([("dm_db_wait_stats", rows), ("dm_os_wait_stats", rows)])
        sql.wait_stats(conn, sql.AZURE_SQL_DATABASE)
        sql.wait_stats(conn, 3)
        self.assertIn("dm_db_wait_stats", conn.executed[0][0])
        self.assertIn("dm_os_wait_stats", conn.executed[1][0])

    def test_blocking_sample_keeps_no_sql_text(self):
        conn = FakeConn([("dm_exec_requests", [("LCK_M_U", 1500, "link-report"), ("LCK_M_U", 200, "link-report")])])
        b = sql.blocking(conn)
        self.assertEqual(b, {"blocked": 2, "max_wait_ms": 1500, "by_wait_type": {"LCK_M_U": 2},
                             "by_database": {"link-report": 2}})
        self.assertNotIn("sql_handle", conn.executed[0][0].lower())

    def test_deadlocks_drop_input_buffers_unless_asked(self):
        xml_text = (FIXTURES / "deadlock-report.xml").read_text(encoding="utf-8")
        conn = FakeConn([("xml_deadlock_report", [(xml_text, datetime(2026, 11, 11, 17, 5))])])
        dl = sql.deadlocks(conn, datetime(2026, 11, 11, tzinfo=timezone.utc), 3)
        self.assertEqual(dl["count"], 1)
        self.assertEqual(dl["events"][0]["databases"], ["link-report"])
        self.assertEqual(dl["events"][0]["objects"], ["link-report.dbo.ReportEntry", "link-report.dbo.ReportSchedule"])
        self.assertEqual(dl["events"][0]["victims"], 1)
        self.assertNotIn("Patient/123", json.dumps(dl))
        self.assertIn("Patient/123", sql.deadlocks(conn, datetime(2026, 11, 11), 3, keep_xml=True)["xml"][0])


# --------------------------------------------------------------------------------------------
# Mongo
# --------------------------------------------------------------------------------------------

class MongoTests(unittest.TestCase):
    def test_resolve_collection_ignores_case(self):
        db = FakeMongoDb({"PatientReportingEvaluationStatus": FakeCollection([])})
        self.assertEqual(mongo.resolve_collection(db, "patientReportingEvaluationStatus"),
                         "PatientReportingEvaluationStatus")
        self.assertIsNone(mongo.resolve_collection(db, "missing"))

    def test_sizes_from_collstats_command_or_unavailable(self):
        u = Unavailable()
        db = FakeMongoDb({"c": FakeCollection([], count=40)}, stats={"size": 4000, "totalIndexSize": 800})
        self.assertEqual(mongo.collection_size(db, "c", u, "mongo"), {"rows": 40, "data_bytes": 4000, "index_bytes": 800})
        cosmos = FakeMongoDb({"c": FakeCollection([], count=40)}, stats=None)
        self.assertEqual(mongo.collection_size(cosmos, "c", u, "mongo")["data_bytes"], None)
        self.assertEqual(u.items[0]["item"], "c sizes")

    def test_report_facts_use_find_not_aggregate(self):
        docs = [{"reports": [{"reportTrackingId": R1, "reportable": True}]},
                {"reports": [{"reportTrackingId": R1.upper(), "reportable": False},
                             {"reportTrackingId": "33333333-3333-3333-3333-333333333333", "reportable": True}]},
                {"reports": [{"reportTrackingId": R2, "reportable": None}]}]
        coll = FakeCollection(docs)
        f = mongo.report_facts(FakeMongoDb({"c": coll}), "c", "reports.reportTrackingId", [R1, R2])
        self.assertEqual(f["by_report"], {R1: 2, R2: 1})
        self.assertEqual((f["evaluated"], f["reportable"], f["share"]), (2, 1, 0.5))
        query, projection = coll.queries[0]
        self.assertIn(R1.upper(), query["reports.reportTrackingId"]["$in"])
        self.assertEqual(projection, {"reports.reportTrackingId": 1, "reports.reportable": 1})
        self.assertFalse(hasattr(coll, "aggregate"))

    def test_cosmos_detection(self):
        self.assertTrue(mongo.is_cosmos("mongodb://x.mongo.cosmos.azure.com:10255/?ssl=true"))
        self.assertFalse(mongo.is_cosmos("mongodb://localhost:17017"))


# --------------------------------------------------------------------------------------------
# Kafka
# --------------------------------------------------------------------------------------------

class KafkaTests(unittest.TestCase):
    def test_client_config_from_targets(self):
        conf = kafka.client_config({"bootstrap_servers": "b:9094", "security_protocol": "SASL_PLAINTEXT",
                                    "sasl_mechanism": "PLAIN", "username": "u", "password": "p"})
        self.assertEqual(conf["sasl.username"], "u")
        self.assertEqual(conf["security.protocol"], "SASL_PLAINTEXT")

    def test_metadata_and_watermarks(self):
        admin = FakeAdmin({"ReportScheduled": 3, "__consumer_offsets": 50})
        meta = kafka.topic_metadata(admin)
        self.assertEqual(meta, {"ReportScheduled": {"partitions": [0, 1, 2], "replication": 3}})
        consumer = FakeConsumer({("ReportScheduled", 0): (5, 10), ("ReportScheduled", 1): (0, 0),
                                 ("ReportScheduled", 2): (0, 7)})
        marks = kafka.watermarks(consumer, "ReportScheduled", [0, 1, 2])
        self.assertEqual(kafka.summarize_watermarks(marks), {"high_watermark": 17, "messages_held": 12})

    def test_sampling_reads_newest_messages_and_counts_headers(self):
        marks = {0: (0, 100), 1: (3, 3)}
        consumer = FakeConsumer({}, [FakeMessage(0, 98, b"key", b"value-1"),
                                     FakeMessage(0, 99, None, b"value-22", [("h", b"vv")])])
        s = kafka.sample_sizes(consumer, "T", marks, per_partition=2)
        self.assertEqual(consumer.assigned, [("T", 0, 98)])  # empty partition skipped
        self.assertEqual(s["sampled"], 2)
        self.assertAlmostEqual(s["avg_message_bytes"], ((3 + 7) + (0 + 8 + 1 + 2)) / 2)

    def test_consumer_lag_skips_our_own_groups(self):
        admin = FakeAdmin({}, groups={"report": {("ReportScheduled", 0): 8, ("ReportScheduled", 1): -1001},
                                      "capacity-snapshot-x": {("ReportScheduled", 0): 0}})
        lag = kafka.consumer_lag(admin, {("ReportScheduled", 0): 10, ("ReportScheduled", 1): 4})
        self.assertEqual(lag, {"report": {"ReportScheduled": 2}})


class FakeResponse:
    def __init__(self, payload=None, status=200):
        self.status_code = 204 if payload is None and status == 200 else status
        self._payload = payload
        self.content = b"" if payload is None else json.dumps(payload).encode()

    def json(self):
        return self._payload

    def raise_for_status(self):
        if self.status_code >= 400:
            raise RuntimeError(f"HTTP {self.status_code}")


class FakeHttp:
    """Routes (method, path) to a payload, an int status, or a callable returning one; records every call.

    A route ending in "*" matches any path with that prefix (for the generated consumer instance name).
    """

    def __init__(self, base, routes):
        self.base, self.routes, self.calls = base, routes, []

    def request(self, method, url, **kw):
        self.calls.append((method, url, kw))
        assert url.startswith(self.base), url  # never a link the proxy handed back
        path = url[len(self.base):]
        value = self.routes.get((method, path))
        if value is None:
            value = next((v for (m, k), v in self.routes.items()
                          if m == method and k.endswith("*") and path.startswith(k[:-1])), None)
        if value is None:
            return FakeResponse(status=404)
        value = value(path, kw) if callable(value) else value
        return FakeResponse(status=value) if isinstance(value, int) else FakeResponse(value)


INTERNAL = "http://rest-proxy:8082"  # the proxy's in-cluster name, as it appears in response links
BASE = "http://localhost:8082"


def page(items, nxt=None):
    return {"metadata": {"self": f"{INTERNAL}/whatever", "next": nxt}, "data": items}


def b64(data: bytes) -> str:
    import base64  # noqa: PLC0415
    return base64.b64encode(data).decode()


class RestProxyTests(unittest.TestCase):
    def proxy(self, routes, **cfg):
        routes.setdefault(("GET", "/v3/clusters"), page([{"cluster_id": "c1"}]))
        http = FakeHttp(BASE, routes)
        return kafka_rest.RestProxyKafka({"rest_proxy_url": BASE + "/", **cfg}, http=http, max_polls=5), http

    def test_topics_skip_internal_and_follow_next_on_our_own_host(self):
        k, http = self.proxy({
            ("GET", "/v3/clusters/c1/topics"): page(
                [{"topic_name": "ReportScheduled", "is_internal": False, "partitions_count": 3, "replication_factor": 3},
                 {"topic_name": "__consumer_offsets", "is_internal": True, "partitions_count": 50,
                  "replication_factor": 3}], nxt=f"{INTERNAL}/v3/clusters/c1/topics?page_token=2"),
            ("GET", "/v3/clusters/c1/topics?page_token=2"): page(
                [{"topic_name": "_confluent-x", "is_internal": True, "partitions_count": 1, "replication_factor": 1}]),
        })
        self.assertEqual(k.topics(), {"ReportScheduled": {"partitions": [0, 1, 2], "replication": 3}})
        self.assertEqual(len(http.calls), 3)  # clusters, topics, and the next page on our own host

    def test_cluster_id(self):
        k, _ = self.proxy({("GET", "/v3/clusters"): page([{"cluster_id": "a"}, {"cluster_id": "b"}])})
        with self.assertRaises(ValueError):
            _ = k.cluster
        k, http = self.proxy({("GET", "/v3/clusters"): 500}, rest_proxy_cluster_id="given")
        self.assertEqual(k.cluster, "given")
        self.assertEqual(http.calls, [])

    def test_credentials_are_refused_in_the_url(self):
        with self.assertRaises(ValueError):
            kafka_rest.RestProxyKafka({"rest_proxy_url": "http://u:p@localhost:8082"}, http=object())

    def test_watermarks_and_retention(self):
        k, _ = self.proxy({
            ("GET", "/topics/ReportScheduled/partitions/0/offsets"): {"beginning_offset": 5, "end_offset": 10},
            ("GET", "/topics/ReportScheduled/partitions/1/offsets"): {"beginning_offset": 0, "end_offset": 7},
            ("GET", "/v3/clusters/c1/topics/-/configs"): page(
                [{"topic_name": "ReportScheduled", "name": "retention.ms", "value": "604800000"},
                 {"topic_name": "ReportScheduled", "name": "cleanup.policy", "value": "delete"},
                 {"topic_name": "Other", "name": "retention.ms", "value": "1"}]),
        })
        marks = k.watermarks("ReportScheduled", [0, 1])
        self.assertEqual(kafka.summarize_watermarks(marks), {"high_watermark": 17, "messages_held": 12})
        self.assertEqual(k.retention_ms(["ReportScheduled", "Missing"]),
                         {"ReportScheduled": 604800000, "Missing": None})

    def test_consumer_lag(self):
        k, _ = self.proxy({
            ("GET", "/v3/clusters/c1/consumer-groups"): page(
                [{"consumer_group_id": g} for g in ("report", "capacity-snapshot-x", "gone")]),
            ("GET", "/v3/clusters/c1/consumer-groups/report/lags"): page(
                [{"topic_name": "ReportScheduled", "partition_id": 0, "current_offset": 8, "log_end_offset": 9},
                 {"topic_name": "ReportScheduled", "partition_id": 1, "current_offset": 1, "log_end_offset": 4},
                 {"topic_name": "Other", "partition_id": 0, "current_offset": -1, "log_end_offset": 4}]),
            # "gone" has no lag rows: 404, skipped
        })
        # partition 0 uses the high watermark just read (10); partition 1 falls back to the proxy's log end (4)
        self.assertEqual(k.consumer_lag({("ReportScheduled", 0): 10}), {"report": {"ReportScheduled": 5}})

    def test_sampling_assigns_seeks_reads_and_deletes_the_instance(self):
        batches = [[], [{"topic": "T", "partition": 0, "offset": 98, "key": b64(b"key"), "value": b64(b"value-1")}],
                   [{"topic": "T", "partition": 0, "offset": 99, "key": None, "value": b64(b"value-22")}]]
        seen = {}

        def post(path, kw):
            if path.count("/") == 2:  # /consumers/{group}: create the instance
                seen["create"] = (path, kw["json"])
                return {"instance_id": kw["json"]["name"], "base_uri": f"{INTERNAL}/consumers/x/instances/x"}
            seen[path.rsplit("/", 1)[1]] = (path, kw["json"])  # assignments, positions
            return None

        def delete(path, kw):
            seen["deleted"] = (path, None)

        k, http = self.proxy({
            ("POST", "/consumers/*"): post,
            ("GET", "/consumers/*"): lambda path, kw: batches.pop(0) if batches else [],
            ("DELETE", "/consumers/*"): delete,
        })
        s = k.sample_sizes("T", {0: (0, 100), 1: (3, 3)}, per_partition=2)
        self.assertEqual(s, {"sampled": 2, "avg_message_bytes": ((3 + 7) + 8) / 2})
        create_path, create_body = seen["create"]
        name = create_body["name"]
        self.assertTrue(name.startswith("capacity-snapshot-"))
        self.assertEqual(create_path, f"/consumers/{name}")
        self.assertEqual((create_body["format"], create_body["auto.commit.enable"]), ("binary", "false"))
        inst = f"/consumers/{name}/instances/{name}"
        self.assertEqual(seen["assignments"], (inst + "/assignments", {"partitions": [{"topic": "T", "partition": 0}]}))
        self.assertEqual(seen["positions"][1], {"offsets": [{"topic": "T", "partition": 0, "offset": 98}]})
        self.assertEqual(k.sample_sizes("Empty", {0: (4, 4)}, 2), {"sampled": 0, "avg_message_bytes": None})
        k.close()
        self.assertEqual(seen["deleted"][0], inst)
        self.assertFalse(any(INTERNAL in url for _, url, _ in http.calls))

    def test_sampling_stops_at_the_poll_limit(self):
        k, http = self.proxy({("POST", "/consumers/*"): 204, ("GET", "/consumers/*"): []})
        k._instance = "/consumers/g/instances/g"
        self.assertEqual(k.sample_sizes("T", {0: (0, 10)}, 2), {"sampled": 0, "avg_message_bytes": None})
        self.assertEqual(sum(1 for _, url, _ in http.calls if url.endswith("/records")), 5)


# --------------------------------------------------------------------------------------------
# Loki and cloud
# --------------------------------------------------------------------------------------------

class LokiTests(unittest.TestCase):
    def test_query_and_parse(self):
        self.assertEqual(loki.query_text('{app="link-cloud"}', "component", 3600),
                         'sum by (component) (bytes_over_time({app="link-cloud"}[3600s]))')
        payload = json.loads((FIXTURES / "loki-bytes-over-time.json").read_text(encoding="utf-8"))
        self.assertEqual(loki.parse_vector(payload, "component"),
                         {"Report": 1048576.0, "DataAcquisition": 4194304.0, "unlabelled": 512.0})
        with self.assertRaises(ValueError):
            loki.parse_vector({"status": "error", "error": "bad"}, "component")

    def test_bytes_by_group_sends_window_and_auth(self):
        payload = json.loads((FIXTURES / "loki-bytes-over-time.json").read_text(encoding="utf-8"))
        calls = []

        def http_get(url, params, headers, timeout):
            calls.append((url, params, headers))
            return types.SimpleNamespace(raise_for_status=lambda: None, json=lambda: payload)

        since = datetime(2026, 11, 11, 17, tzinfo=timezone.utc)
        out = loki.bytes_by_group({"url": "http://loki:3100/", "org_id": "t1"}, '{app="link-cloud"}', "component",
                                  since, since + timedelta(minutes=30), http_get)
        self.assertEqual(out["window_s"], 1800)
        self.assertEqual(calls[0][0], "http://loki:3100/loki/api/v1/query")
        self.assertIn("[1800s]", calls[0][1]["query"])
        self.assertEqual(calls[0][2], {"X-Scope-OrgID": "t1"})


class CloudTests(unittest.TestCase):
    def test_cosmos_metrics_from_az_output(self):
        def run(args):
            name = "az-throttled-requests.json" if "TotalRequests" in args else "az-total-request-units.json"
            self.assertIn("--resource", args)
            return (FIXTURES / name).read_text(encoding="utf-8")

        m = cloud.cosmos_metrics("/subscriptions/x/resourceGroups/y/providers/Microsoft.DocumentDB/databaseAccounts/z",
                                 datetime(2026, 11, 11, 17, tzinfo=timezone.utc),
                                 datetime(2026, 11, 11, 18, tzinfo=timezone.utc), run)
        self.assertEqual(m, {"request_units": 4600.5, "peak_ru_per_minute": 3400.5, "throttled_requests": 7,
                             "peak_throttled_per_minute": 7})

    def test_replicas_from_docker_and_kubectl(self):
        self.assertEqual(cloud.docker_replicas(lambda a: "report\nreport\nmeasureeval\n\n"),
                         {"measureeval": {"running": 1}, "report": {"running": 2}})

        def run(args):
            name = "kubectl-hpa.json" if "hpa" in args else "kubectl-deployments.json"
            self.assertIn("link-dev", args)
            return (FIXTURES / name).read_text(encoding="utf-8")

        r = cloud.replicas({"provider": "kubectl", "namespace": "link-dev"}, run)
        self.assertEqual(r["measureeval"], {"desired": 10, "ready": 9, "hpa_min": 4, "hpa_max": 20})
        self.assertIsNone(cloud.replicas({"provider": "none"}))


# --------------------------------------------------------------------------------------------
# Targets
# --------------------------------------------------------------------------------------------

class TargetsTests(unittest.TestCase):
    def args(self, **kw):
        p = argparse.ArgumentParser()
        targets_mod.add_arguments(p)
        a = p.parse_args([])
        for k, v in kw.items():
            setattr(a, k, v)
        return a

    def test_env_substitution_and_overrides(self):
        with tempfile.TemporaryDirectory() as d:
            env = Path(d) / ".env"
            env.write_text("LINK_DB_PASS=secret-from-env\nKAFKA_SASL_CLIENT_USER=u\nKAFKA_SASL_CLIENT_PASSWORD=p\n",
                           encoding="utf-8")
            t = targets_mod.resolve(self.args(env_file=str(env), report_db_name="link-report-2",
                                              kafka_broker="broker:9092", environment="lcg"))
        self.assertIn("PWD=secret-from-env", t["sql"]["connection_string"])
        self.assertEqual(t["sql"]["databases"]["report"], "link-report-2")
        self.assertEqual(t["sql"]["databases"]["dataacquisition"], "link-dataacquisition")
        self.assertEqual(t["kafka"]["bootstrap_servers"], "broker:9092")
        self.assertEqual(t["environment"], "lcg")

    def test_unresolved_variable_is_named_not_shown(self):
        with tempfile.TemporaryDirectory() as d:
            targets = Path(d) / "t.json"
            targets.write_text(json.dumps({"sql": {"connection_string": "PWD=${NOT_SET_ANYWHERE_123}"}}),
                               encoding="utf-8")
            with self.assertRaises(ValueError) as e:
                targets_mod.resolve(self.args(targets=targets, env_file=str(Path(d) / "none.env")))
        self.assertIn("NOT_SET_ANYWHERE_123", str(e.exception))

    def test_server_key_has_no_credentials(self):
        t = {"sql": {"connection_string": "Server=tcp:db.example.net,1433;UID=sa;PWD=x", "connection_strings": {}}}
        self.assertEqual(targets_mod.sql_server_key(t, "report"), "tcp:db.example.net,1433")

    def test_rest_proxy_flags(self):
        with tempfile.TemporaryDirectory() as d:
            env = Path(d) / ".env"
            env.write_text("LINK_DB_PASS=x\nKAFKA_SASL_CLIENT_USER=u\nKAFKA_SASL_CLIENT_PASSWORD=p\n", encoding="utf-8")
            t = targets_mod.resolve(self.args(env_file=str(env), kafka_rest_proxy_url="http://localhost:8082",
                                              kafka_rest_proxy_username="ru", kafka_rest_proxy_password="rp",
                                              kafka_rest_proxy_cluster_id="c1"))
        self.assertEqual({k: t["kafka"][k] for k in ("rest_proxy_url", "rest_proxy_username", "rest_proxy_password",
                                                      "rest_proxy_cluster_id")},
                         {"rest_proxy_url": "http://localhost:8082", "rest_proxy_username": "ru",
                          "rest_proxy_password": "rp", "rest_proxy_cluster_id": "c1"})


# --------------------------------------------------------------------------------------------
# snapshot.take end to end, against fakes
# --------------------------------------------------------------------------------------------

class FakeSession(snapshot.Session):
    def __init__(self, conns, mongo_client, kafka_clients, targets):
        super().__init__(targets, Unavailable())
        self._sql = conns
        self._mongo = mongo_client
        self._kafka = kafka_clients

    def sql(self, service):
        if service not in self._sql:
            self.unavailable.add("sql", service, "not configured in test")
            self._sql[service] = None
        return self._sql[service]


class SnapshotTests(unittest.TestCase):
    def build(self):
        server = [("SERVERPROPERTY('EngineEdition')", [(3, "16.0", "Developer", None, None, "link-report")]),
                  ("dm_os_wait_stats", [("LCK_M_X", 1, 50, 0)]), ("dm_exec_requests", []),
                  ("xml_deadlock_report", [])]
        report = FakeConn([("dm_db_partition_stats", [("dbo", "ReportSchedule", 2, 8192, 0),
                                                       ("dbo", "ReportEntry", 7, 8192, 8192)]),
                           ("FROM dbo.ReportSchedule WHERE", [
                               (R1, "f", "Daily", datetime(2026, 11, 10), datetime(2026, 11, 11), "Submitted",
                                datetime(2026, 11, 11, 1), datetime(2026, 11, 11, 3))]),
                           ("COUNT_BIG", lambda q, p: [(R1, 7 if "ReportEntry" in q else 1)]),
                           *server])
        census = FakeConn([("PatientEncounters", [(8, 7)]), *server])
        db = FakeMongoDb({"patientReportingEvaluationStatus": FakeCollection(
            [{"reports": [{"reportTrackingId": R1, "reportable": True}]}], count=5)}, stats={"size": 10, "totalIndexSize": 2})
        client = _Client(db)
        consumer = FakeConsumer({("ReportScheduled", 0): (0, 4)}, [FakeMessage(0, 3)])
        admin = FakeAdmin({"ReportScheduled": 1}, groups={"report": {("ReportScheduled", 0): 3}})
        admin.describe_configs = None  # retention lookup fails -> recorded as unavailable
        targets = {"environment": "test", "sql": {"connection_string": "Server=s;", "connection_strings": {}},
                   "mongo": {"connection_string": "mongodb://localhost", "databases": {"measureeval": "m"}},
                   "kafka": {"bootstrap_servers": "b"}, "loki": {}, "cosmos": {}, "replicas": {"provider": "none"}}
        return FakeSession({"report": report, "census": census}, client, kafka.NativeKafka(admin, consumer), targets)

    def stores(self):
        return [
            {"id": "report.ReportSchedule", "kind": "sql", "service": "Report", "grain": "report", "anchor": "report",
             "location": {"service": "report", "table": "dbo.ReportSchedule", "attribution": {"key": "Id"}}},
            {"id": "report.Missing", "kind": "sql", "service": "Report", "grain": "report", "anchor": "report",
             "location": {"service": "report", "table": "dbo.NotThere"}},
            {"id": "census.PatientEncounter", "kind": "sql", "service": "Census", "grain": "census_patient",
             "anchor": "record_age", "location": {"service": "census", "table": "dbo.PatientEncounters"}},
            {"id": "measureeval.PRES", "kind": "mongo", "service": "MeasureEval", "grain": "patient_report",
             "anchor": "report", "location": {"service": "measureeval", "collection": "patientReportingEvaluationStatus",
                                              "report_key": "reports.reportTrackingId"}},
            {"id": "kafka.ReportScheduled", "kind": "kafka", "service": "Kafka", "grain": "report",
             "anchor": "own_retention", "location": {"topics": ["ReportScheduled"]}},
        ]

    def test_full_snapshot(self):
        session = self.build()
        since = datetime(2026, 11, 11, tzinfo=timezone.utc)
        snap = snapshot.take(session, self.stores(), "full", "after", since)
        self.assertEqual(snap["schema"], 1)
        self.assertEqual(snap["stores"]["report.ReportSchedule"]["by_report"], {R1: 1})
        self.assertNotIn("report.Missing", snap["stores"])
        self.assertEqual(snap["run"]["by_cadence"], {"daily": {"reports": 1, "entries": 7}})
        self.assertEqual(snap["run"]["census"], {"encounters": 8, "patients": 7})
        self.assertEqual(snap["run"]["reportable"]["share"], 1.0)
        self.assertEqual(snap["stores"]["measureeval.PRES"]["by_report"], {R1: 1})
        self.assertEqual(snap["kafka"]["ReportScheduled"]["high_watermark"], 4)
        self.assertEqual(snap["consumer_lag"], {"report": {"ReportScheduled": 1}})
        self.assertEqual(list(snap["contention"]["sql"]), ["s"])  # one server, keyed without credentials
        self.assertEqual(sorted(list(snap["contention"]["sql"].values())[0]["services"]), ["census", "report"])
        items = {(u["source"], u["item"]) for u in snap["unavailable"]}
        self.assertIn(("sql", "report.Missing"), items)
        self.assertIn(("kafka", "retention.ms"), items)
        self.assertIn(("cloud", "Cosmos RU and 429 metrics"), items)
        json.dumps(snap)  # serialisable

    def test_light_snapshot_skips_run_facts_and_sampling(self):
        snap = snapshot.take(self.build(), self.stores(), "light", None)
        self.assertNotIn("run", snap)
        self.assertNotIn("by_report", snap["stores"]["report.ReportSchedule"])
        self.assertNotIn("avg_message_bytes", snap["kafka"]["ReportScheduled"])
        self.assertNotIn("context", snap)

    def test_full_snapshot_through_the_rest_proxy(self):
        session = self.build()
        session.targets["kafka"] = {"rest_proxy_url": BASE}
        session._kafka = kafka_rest.RestProxyKafka(session.targets["kafka"], max_polls=2, http=FakeHttp(BASE, {
            ("GET", "/v3/clusters"): page([{"cluster_id": "c1"}]),
            ("GET", "/v3/clusters/c1/topics"): page([{"topic_name": "ReportScheduled", "is_internal": False,
                                                      "partitions_count": 1, "replication_factor": 3}]),
            ("GET", "/topics/ReportScheduled/partitions/0/offsets"): {"beginning_offset": 0, "end_offset": 4},
            ("POST", "/consumers/*"): lambda path, kw: {"instance_id": "x", "base_uri": f"{INTERNAL}/x"}
            if path.count("/") == 2 else None,
            ("GET", "/consumers/*"): [{"topic": "ReportScheduled", "partition": 0, "offset": 3,
                                       "key": b64(b"k"), "value": b64(b"vvvv")}],
            ("DELETE", "/consumers/*"): 204,
            ("GET", "/v3/clusters/c1/topics/-/configs"): page(
                [{"topic_name": "ReportScheduled", "name": "retention.ms", "value": "604800000"}]),
            ("GET", "/v3/clusters/c1/consumer-groups"): page([{"consumer_group_id": "report"}]),
            ("GET", "/v3/clusters/c1/consumer-groups/report/lags"): page(
                [{"topic_name": "ReportScheduled", "partition_id": 0, "current_offset": 3, "log_end_offset": 4}]),
        }))
        snap = snapshot.take(session, self.stores(), "full", "after", datetime(2026, 11, 11, tzinfo=timezone.utc))
        topic = snap["kafka"]["ReportScheduled"]
        self.assertEqual((topic["high_watermark"], topic["retention_ms"]), (4, 604800000))
        self.assertEqual((topic["avg_message_bytes"], topic["size_basis"]), (5, "key+value"))
        self.assertEqual(snap["consumer_lag"], {"report": {"ReportScheduled": 1}})
        self.assertEqual(snap["context"]["kafka"]["via"], "rest_proxy")
        self.assertNotIn("kafka", {u["source"] for u in snap["unavailable"]})

    def test_deadlock_xml_is_split_out(self):
        snap = {"contention": {"sql": {"s": {"deadlocks": {"count": 1, "events": [], "xml": ["<deadlock/>"]}}}}}
        self.assertEqual(snapshot.split_deadlock_xml(snap), ["<deadlock/>"])
        self.assertNotIn("xml", snap["contention"]["sql"]["s"]["deadlocks"])


class _Client:
    def __init__(self, db):
        self.db = db

    def __getitem__(self, name):
        return self.db

    def server_info(self):
        return {"version": "6.0"}

    def close(self):
        pass


if __name__ == "__main__":
    unittest.main()
