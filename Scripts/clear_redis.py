"""
Clears keys from a self-hosted (non-managed) Redis instance running in Azure.

Accepts either connection string style you are likely to have been handed:

  URL style              redis://:password@myhost.eastus.cloudapp.azure.com:6379/0
                         rediss://user:password@myhost.eastus.cloudapp.azure.com:6380/0
  StackExchange style    myhost.eastus.cloudapp.azure.com:6380,password=secret,ssl=True,abortConnect=False
  Bare host:port         myhost.eastus.cloudapp.azure.com:6379

In StackExchange style the password may also be dropped in on its own, without the
"password=" prefix - myhost:6380,secret,ssl=True works the same as password=secret.

Nothing is deleted unless you pass --yes; without it the script connects, reports
what it *would* remove, and exits. Pass the connection string with
--connection-string or put it in the REDIS_CONNECTION_STRING environment variable
so it stays out of your shell history.

Usage:
    # Dry run - show what is there
    python clear_redis.py --connection-string "rediss://:pw@host:6380/0"

    # Delete every key in the selected database
    python clear_redis.py --connection-string "..." --yes

    # Delete every key in every database on the server
    python clear_redis.py --connection-string "..." --all-dbs --yes

    # Delete only keys matching a pattern (uses SCAN, never blocks the server)
    python clear_redis.py --connection-string "..." --pattern "session:*" --yes

    # Self-signed / internal CA on the Redis host
    python clear_redis.py --connection-string "rediss://..." --insecure --yes

Requires: pip install redis
"""

import argparse
import os
import sys

import redis

# Deleting in batches keeps each round trip small and lets us print progress on
# large keyspaces instead of appearing to hang.
SCAN_BATCH = 500

# StackExchange.Redis option names we understand, plus the .NET-only ones we
# knowingly ignore. Anything outside this set is taken to be a bare password.
KNOWN_OPTIONS = {
    "password", "user", "username", "ssl", "defaultdatabase",
    "abortconnect", "connecttimeout", "synctimeout", "connectretry",
    "allowadmin", "name", "clientname", "sslprotocols", "responsetimeout",
}


def parse_connection_string(conn_str, insecure=False):
    """Turns any of the supported connection string forms into redis.Redis kwargs."""
    conn_str = conn_str.strip()
    lowered = conn_str.lower()

    if lowered.startswith("redis://") or lowered.startswith("rediss://"):
        kwargs = {"url": conn_str}
        if insecure and lowered.startswith("rediss://"):
            kwargs["ssl_cert_reqs"] = None
        return "url", kwargs

    # StackExchange.Redis style: host:port followed by comma separated options.
    parts = [p.strip() for p in conn_str.split(",") if p.strip()]
    if not parts:
        sys.exit("Connection string is empty")

    endpoint = parts[0]
    if "=" in endpoint and ":" not in endpoint:
        sys.exit(f"Could not find host:port at the start of the connection string: {conn_str!r}")

    if ":" in endpoint:
        host, _, port_text = endpoint.rpartition(":")
        try:
            port = int(port_text)
        except ValueError:
            sys.exit(f"Could not parse port from {endpoint!r}")
    else:
        host, port = endpoint, 6379

    kwargs = {"host": host, "port": port, "db": 0}

    for option in parts[1:]:
        key, separator, value = option.partition("=")
        key = key.strip().lower()
        value = value.strip()

        # A token is only an option if its name is one we know. Anything else is
        # treated as a bare password, because that is what people actually paste:
        # the password alone, without the "password=" prefix. Matching on the known
        # names (rather than just on "does it contain '='") also protects Azure
        # access keys, which are base64 and routinely end in '=' - "abc123=" would
        # otherwise parse as an unknown option named "abc123" and be dropped.
        if not separator or key not in KNOWN_OPTIONS:
            if "password" in kwargs:
                print(f"Ignoring unrecognised connection string option: {key or option!r}")
            else:
                kwargs["password"] = option
            continue

        if key == "password":
            kwargs["password"] = value
        elif key in ("user", "username"):
            kwargs["username"] = value
        elif key == "ssl":
            kwargs["ssl"] = value.lower() == "true"
        elif key == "defaultdatabase":
            kwargs["db"] = int(value)
        # abortConnect, connectTimeout, syncTimeout etc. are .NET client options
        # with no redis-py equivalent, so they are ignored on purpose.

    # 6380 is the conventional TLS port for Redis in Azure. Connecting to it in
    # plaintext just stalls until timeout, which reads like a firewall problem
    # rather than the missing ssl=True it actually is.
    if "ssl" not in kwargs and port == 6380:
        print("Port 6380 with no ssl option in the connection string; assuming TLS.")
        kwargs["ssl"] = True

    if kwargs.get("ssl") and insecure:
        kwargs["ssl_cert_reqs"] = None

    return "kwargs", kwargs


def connect(conn_str, insecure, db_override):
    style, kwargs = parse_connection_string(conn_str, insecure=insecure)

    if style == "url":
        url = kwargs.pop("url")
        client = redis.Redis.from_url(url, decode_responses=True, socket_timeout=30, **kwargs)
        if db_override is not None:
            # from_url always takes the db from the URL path and ignores a db
            # kwarg, so override it on the pool instead.
            client.connection_pool.connection_kwargs["db"] = db_override
    else:
        if db_override is not None:
            kwargs["db"] = db_override
        client = redis.Redis(decode_responses=True, socket_timeout=30, **kwargs)

    return client


def client_for_db(base, db):
    """Clones a client onto another database.

    Cheaper alternatives (SELECT on the shared client) are not safe here: redis-py
    hands each command a connection from the pool, so a SELECT and the DBSIZE that
    follows it can land on different connections. A separate client per database
    keeps the selection unambiguous, and copying the pool's kwargs preserves TLS
    and credentials.
    """
    kwargs = dict(base.connection_pool.connection_kwargs)
    kwargs["db"] = db
    pool = base.connection_pool.__class__(
        connection_class=base.connection_pool.connection_class, **kwargs
    )
    return redis.Redis(connection_pool=pool)


def describe_server(client):
    """Prints who we are connected to and how many keys live in each database.

    Returns the parsed keyspace, or None if the server would not tell us - the two
    cases mean very different things and the caller must not confuse them.
    """
    connection = client.connection_pool.connection_kwargs
    host = connection.get("host", "?")
    port = connection.get("port", "?")
    db = connection.get("db", 0)
    tls = "yes" if issubclass(client.connection_pool.connection_class, redis.SSLConnection) else "no"

    print(f"Connected to {host}:{port} (db {db}, TLS {tls})")

    try:
        info = client.info("server")
        print(f"Redis version: {info.get('redis_version', 'unknown')}, mode: {info.get('redis_mode', 'unknown')}")
    except redis.RedisError as e:
        print(f"Could not read server info: {e}")

    try:
        keyspace = client.info("keyspace")
    except redis.RedisError as e:
        print(f"Could not read keyspace info: {e}")
        return None

    if keyspace:
        print("Keys per database (INFO keyspace omits empty databases):")
        for name in sorted(keyspace):
            stats = keyspace[name]
            if isinstance(stats, dict):
                print(f"  {name}: {stats.get('keys', '?')} keys")
            else:
                print(f"  {name}: {stats}")
    else:
        print("Keyspace is empty (no database holds any keys)")

    return keyspace


def databases_with_keys(client, keyspace):
    """Returns the db numbers that contain keys.

    Normally that is just parsing 'db3' -> 3 out of INFO keyspace. When INFO is
    restricted (it often is on hardened self-hosted servers) we probe each database
    instead, so --all-dbs does not quietly report 'nothing to do' and clear nothing.
    """
    if keyspace is not None:
        return sorted(int(name[2:]) for name in keyspace if name.startswith("db") and name[2:].isdigit())

    print("Falling back to probing each database directly...")
    try:
        count = int(client.config_get("databases").get("databases", 16))
    except (redis.RedisError, ValueError, AttributeError):
        count = 16
        print(f"  CONFIG GET is unavailable too; assuming the default {count} databases")

    found = []
    for db in range(count):
        try:
            size = client_for_db(client, db).dbsize()
        except redis.RedisError as e:
            print(f"  db {db}: could not read size ({e}), skipping")
            continue
        if size:
            print(f"  db {db}: {size} keys")
            found.append(db)
    return found


def delete_by_pattern(client, pattern, dry_run):
    """SCAN + UNLINK in batches. Safe on a live server: never blocks like KEYS does."""
    state = {"unlink": True}
    matched = 0
    deleted = 0
    batch = []

    for key in client.scan_iter(match=pattern, count=SCAN_BATCH):
        matched += 1

        if dry_run:
            if matched <= 20:
                print(f"  would delete: {key}")
            continue

        batch.append(key)
        if len(batch) >= SCAN_BATCH:
            deleted += _delete_batch(client, batch, state)
            batch = []
            print(f"  deleted {deleted} keys so far...")

    if batch:
        deleted += _delete_batch(client, batch, state)

    if dry_run:
        if matched > 20:
            print(f"  ... and {matched - 20} more")
        print(f"Matched {matched} keys for pattern {pattern!r} (dry run, nothing deleted)")
    else:
        print(f"Deleted {deleted} of {matched} keys matching {pattern!r}")

    return deleted


def _delete_batch(client, keys, state):
    """UNLINK frees memory on a background thread; fall back to DEL on old servers."""
    if state["unlink"]:
        try:
            return client.unlink(*keys)
        except redis.ResponseError:
            state["unlink"] = False
            print("  UNLINK not supported by this server, using DEL")
    return client.delete(*keys)


def flush_database(client, db, dry_run, use_async):
    size = client.dbsize()

    if dry_run:
        print(f"  db {db}: would flush {size} keys")
        return 0

    print(f"  db {db}: flushing {size} keys...")
    try:
        client.flushdb(asynchronous=use_async)
    except redis.ResponseError as e:
        # FLUSHDB is commonly renamed or disabled in hardened deployments.
        sys.exit(
            f"FLUSHDB was rejected by the server ({e}). "
            f"Re-run with --pattern '*' to delete via SCAN instead."
        )

    print(f"  db {db}: done, {client.dbsize()} keys remain")
    return size


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--connection-string", default=os.environ.get("REDIS_CONNECTION_STRING"),
                        help="Redis connection string. Defaults to the REDIS_CONNECTION_STRING environment variable.")
    parser.add_argument("--db", type=int, default=None,
                        help="Database number to clear, overriding whatever the connection string selects.")
    parser.add_argument("--all-dbs", action="store_true",
                        help="Clear every database on the server, not just the selected one.")
    parser.add_argument("--pattern", default=None,
                        help="Only delete keys matching this glob (e.g. 'session:*'). Uses SCAN + UNLINK "
                             "instead of FLUSHDB, so it is safe on a live server and works when FLUSHDB is disabled.")
    parser.add_argument("--async-flush", action="store_true",
                        help="Ask the server to free memory in the background (FLUSHDB ASYNC). Returns faster on large keyspaces.")
    parser.add_argument("--insecure", action="store_true",
                        help="Skip TLS certificate verification. Needed when the Redis VM presents a self-signed or internal-CA cert.")
    parser.add_argument("--yes", action="store_true",
                        help="Actually delete. Without this the script only reports what it would do.")
    args = parser.parse_args()

    if not args.connection_string:
        sys.exit("No connection string. Pass --connection-string or set REDIS_CONNECTION_STRING.")

    if args.all_dbs and args.pattern:
        sys.exit("--all-dbs and --pattern cannot be combined; run the pattern delete against one db at a time with --db.")

    dry_run = not args.yes

    try:
        client = connect(args.connection_string, args.insecure, args.db)
        client.ping()
    except redis.AuthenticationError as e:
        sys.exit(f"Authentication failed: {e}")
    except redis.RedisError as e:
        sys.exit(f"Could not connect: {e}")

    keyspace = describe_server(client)
    print()

    if dry_run:
        print("DRY RUN - nothing will be deleted. Re-run with --yes to actually clear.")
        print()

    if args.pattern:
        delete_by_pattern(client, args.pattern, dry_run)
    elif args.all_dbs:
        targets = databases_with_keys(client, keyspace)
        if not targets:
            print("Nothing to do: no database holds any keys.")
            return
        print(f"Target databases: {', '.join(str(d) for d in targets)}")
        for db in targets:
            flush_database(client_for_db(client, db), db, dry_run, args.async_flush)
    else:
        db = client.connection_pool.connection_kwargs.get("db", 0)

        # Without this it is easy to clear db 0, see "done", and never notice that
        # another database still holds everything you meant to get rid of.
        if keyspace is not None:
            untouched = [other for other in databases_with_keys(client, keyspace) if other != db]
            if untouched:
                names = ", ".join(str(d) for d in untouched)
                if len(untouched) == 1:
                    print(f"Note: db {names} also holds keys and will NOT be cleared. "
                          f"Re-run with --all-dbs to include it.")
                else:
                    print(f"Note: dbs {names} also hold keys and will NOT be cleared. "
                          f"Re-run with --all-dbs to include them.")
            else:
                print(f"db {db} is the only database holding keys.")

        flush_database(client, db, dry_run, args.async_flush)

    print()
    if dry_run:
        print("Dry run complete. Re-run with --yes to delete.")
    else:
        print("Done.")


if __name__ == "__main__":
    main()
