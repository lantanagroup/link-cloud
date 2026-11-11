"""Capture collectors for the LEGPROG-516 metadata volume model (LEGLINK-1369).

Each collector reads one kind of source (SQL Server, MongoDB, Kafka, Loki, cloud context) and writes its
section of a snapshot. Client libraries are imported lazily, so the model and the unit tests run without them.
Every query is read-only.
"""

SCHEMA_VERSION = 1
