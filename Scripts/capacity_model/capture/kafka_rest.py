"""Kafka collector through the Confluent REST proxy: the same facts as `capture.kafka.NativeKafka`, over HTTP.

Use it where the brokers can't be reached directly, such as an AKS cluster reached by `kubectl port-forward`: the
brokers advertise cluster-internal host names, so a client outside the cluster fails after bootstrap ("Broker
transport failure"). The proxy talks to the brokers from inside the cluster, so one forwarded port is enough.

- Topics, `retention.ms` and consumer lag come from the v3 API; watermarks from the v2 partition offsets.
- Message sizes come from a v2 consumer instance that is only ever assigned partitions: it never subscribes and
  never commits, so no consumer group is created or moved. It is deleted on `close()`; the proxy also expires idle
  instances. v2 records carry no headers, so sizes are key + value only (`size_basis`).
- Response links (`self`, `next`, `base_uri`) name the proxy by its in-cluster host, so they are never followed
  as given: every URL is built from the configured base URL.
"""

from __future__ import annotations

import base64
import uuid
from urllib.parse import quote, urlsplit

V2 = "application/vnd.kafka.v2+json"
V2_BINARY = "application/vnd.kafka.binary.v2+json"
GROUP_PREFIX = "capacity-snapshot-"


def _seg(value) -> str:
    return quote(str(value), safe="")


class RestProxyKafka:
    """The collector's Kafka interface over the Confluent REST proxy."""

    via = "rest_proxy"
    size_basis = "key+value"

    def __init__(self, kafka: dict, http=None, timeout: float = 10, max_polls: int = 20):
        url = urlsplit(kafka["rest_proxy_url"])
        if url.username or url.password:  # error text includes URLs, so credentials never stay in one
            raise ValueError("put REST proxy credentials in rest_proxy_username / rest_proxy_password, not the URL")
        self.base = kafka["rest_proxy_url"].rstrip("/")
        self.timeout, self.max_polls = timeout, max_polls
        if http is None:
            import requests  # noqa: PLC0415 - optional dependency
            http = requests.Session()
            if kafka.get("rest_proxy_username"):
                http.auth = (kafka["rest_proxy_username"], kafka.get("rest_proxy_password", ""))
        self.http = http
        self._cluster = kafka.get("rest_proxy_cluster_id") or None
        self._instance: str | None = None

    # -- HTTP -----------------------------------------------------------------------------------

    def _request(self, method: str, path: str, *, ok_missing: bool = False, **kw):
        kw.setdefault("timeout", self.timeout)
        response = self.http.request(method, self.base + path, **kw)
        if ok_missing and response.status_code == 404:
            return None
        response.raise_for_status()
        return response.json() if response.status_code != 204 and response.content else None

    def _list(self, path: str, **kw) -> list[dict]:
        """Every item of a v3 list, following `next` re-based onto our own URL."""
        items = []
        while path:
            page = self._request("GET", path, **kw)
            if page is None:
                return items
            items.extend(page.get("data", []))
            nxt = (page.get("metadata") or {}).get("next")
            if nxt:
                parts = urlsplit(nxt)
                path = parts.path + (f"?{parts.query}" if parts.query else "")
            else:
                path = None
        return items

    @property
    def cluster(self) -> str:
        if self._cluster is None:
            ids = [c["cluster_id"] for c in self._list("/v3/clusters")]
            if len(ids) != 1:
                raise ValueError(f"the REST proxy lists {len(ids)} clusters; set kafka.rest_proxy_cluster_id "
                                 "(or --kafka-rest-proxy-cluster-id)")
            self._cluster = ids[0]
        return self._cluster

    def _v3(self, path: str) -> str:
        return f"/v3/clusters/{_seg(self.cluster)}{path}"

    # -- Collector interface --------------------------------------------------------------------

    def topics(self) -> dict[str, dict]:
        """{topic: {partitions: [ids], replication}} for every non-internal topic."""
        out = {}
        for t in self._list(self._v3("/topics")):
            name = t["topic_name"]
            if t.get("is_internal") or name.startswith("__"):
                continue
            out[name] = {"partitions": list(range(int(t["partitions_count"]))),
                         "replication": int(t["replication_factor"])}
        return out

    def watermarks(self, topic: str, partitions: list[int]) -> dict[int, tuple[int, int]]:
        marks = {}
        for p in partitions:
            o = self._request("GET", f"/topics/{_seg(topic)}/partitions/{p}/offsets", headers={"Accept": V2})
            marks[p] = (int(o["beginning_offset"]), int(o["end_offset"]))
        return marks

    def retention_ms(self, topics: list[str]) -> dict[str, int | None]:
        wanted = set(topics)
        result: dict[str, int | None] = {t: None for t in topics}
        for c in self._list(self._v3("/topics/-/configs")):
            if c.get("name") == "retention.ms" and c.get("topic_name") in wanted:
                result[c["topic_name"]] = int(c["value"]) if c.get("value") is not None else None
        return result

    def consumer_lag(self, high_by_topic_partition: dict[tuple[str, int], int]) -> dict:
        """{group: {topic: lag}}, comparing committed offsets with the high watermarks just read."""
        lag: dict[str, dict[str, int]] = {}
        groups = sorted(g["consumer_group_id"] for g in self._list(self._v3("/consumer-groups")))
        for group in groups:
            if group.startswith(GROUP_PREFIX):
                continue
            for row in self._list(self._v3(f"/consumer-groups/{_seg(group)}/lags"), ok_missing=True):
                current = row.get("current_offset")
                if current is None or int(current) < 0:
                    continue
                key = (row["topic_name"], int(row["partition_id"]))
                high = high_by_topic_partition.get(key, row.get("log_end_offset"))
                if high is None:
                    continue
                lag.setdefault(group, {})
                lag[group][key[0]] = lag[group].get(key[0], 0) + max(0, int(high) - int(current))
        return lag

    def _consumer(self) -> str:
        """Path of this session's consumer instance, created on first use."""
        if self._instance is None:
            name = f"{GROUP_PREFIX}{uuid.uuid4()}"
            self._request("POST", f"/consumers/{_seg(name)}", headers={"Content-Type": V2, "Accept": V2},
                          json={"name": name, "format": "binary", "auto.offset.reset": "earliest",
                                "auto.commit.enable": "false"})
            self._instance = f"/consumers/{_seg(name)}/instances/{_seg(name)}"
        return self._instance

    def sample_sizes(self, topic: str, marks: dict[int, tuple[int, int]], per_partition: int) -> dict:
        """Average key + value bytes over the newest `per_partition` messages of each partition."""
        targets = {p: h - 1 for p, (lo, h) in marks.items() if h > lo}
        if not targets:
            return {"sampled": 0, "avg_message_bytes": None}
        inst = self._consumer()
        self._request("POST", f"{inst}/assignments", headers={"Content-Type": V2},
                      json={"partitions": [{"topic": topic, "partition": p} for p in targets]})
        self._request("POST", f"{inst}/positions", headers={"Content-Type": V2},
                      json={"offsets": [{"topic": topic, "partition": p,
                                         "offset": max(marks[p][0], marks[p][1] - per_partition)}
                                        for p in targets]})
        sizes, done = [], set()
        for _ in range(self.max_polls):
            records = self._request("GET", f"{inst}/records", headers={"Accept": V2_BINARY},
                                    params={"timeout": 1000})
            for r in records or []:
                if r.get("topic") != topic or r.get("partition") not in targets:
                    continue
                sizes.append(len(base64.b64decode(r.get("key") or "")) + len(base64.b64decode(r.get("value") or "")))
                if int(r["offset"]) >= targets[r["partition"]]:
                    done.add(r["partition"])
            if len(done) == len(targets):
                break
        return {"sampled": len(sizes), "avg_message_bytes": sum(sizes) / len(sizes) if sizes else None}

    def close(self) -> None:
        if self._instance is not None:
            try:
                self._request("DELETE", self._instance, headers={"Content-Type": V2})
            except Exception:  # noqa: BLE001 - the proxy expires idle instances anyway
                pass
            self._instance = None
