from __future__ import annotations

import argparse
import sys
import time
from urllib.parse import urlencode, urljoin, urlparse, urlunparse, parse_qsl

import requests

"""
Fetch Patient IDs from a FHIR server and write them to a text file (one ID per line).

Example:
  python get_patient_ids.py \
    --fhir-server-base https://example.com/fhir \
    --count 1000 \
    --output patient_ids.txt \
    --max 200
"""

def normalize_base_url(base: str) -> str:
    # Ensure trailing slash so urljoin works predictably
    return base if base.endswith("/") else base + "/"


def build_initial_url(base: str, page_size: int) -> str:
    # GET <base>/Patient?_elements=id&_count=<page_size>
    base = normalize_base_url(base)
    patient_endpoint = urljoin(base, "Patient")
    query = {"_elements": "id", "_count": str(page_size)}
    return f"{patient_endpoint}?{urlencode(query)}"


def find_next_link(bundle: dict) -> str | None:
    for link in bundle.get("link", []) or []:
        if link.get("relation") == "next" and link.get("url"):
            return link["url"]
    return None


def extract_patient_ids(bundle: dict) -> list[str]:
    ids: list[str] = []
    for entry in bundle.get("entry", []) or []:
        res = entry.get("resource") or {}
        if res.get("resourceType") == "Patient":
            pid = res.get("id")
            if isinstance(pid, str) and pid.strip():
                ids.append(pid.strip())
    return ids


def enforce_count_param(url: str, page_size: int) -> str:
    """
    Some servers ignore/omit _count on next links; optionally enforce it.
    We keep whatever the server gave us, but ensure _count is present.
    """
    p = urlparse(url)
    q = dict(parse_qsl(p.query, keep_blank_values=True))
    q["_count"] = str(page_size)
    new_query = urlencode(q)
    return urlunparse((p.scheme, p.netloc, p.path, p.params, new_query, p.fragment))


def fetch_all_patient_ids(
        start_url: str,
        page_size: int,
        limit: int | None,
        timeout_seconds: float,
        user_agent: str,
        retries: int,
        retry_backoff: float,
) -> list[str]:
    session = requests.Session()
    session.headers.update(
        {
            "Accept": "application/fhir+json, application/json",
            "User-Agent": user_agent,
        }
    )

    url = start_url
    collected: list[str] = []

    while url:
        # Stop early if we've hit limit
        if limit is not None and len(collected) >= limit:
            break
            
        print(f"Fetching page: {url}")

        last_err: Exception | None = None
        for attempt in range(retries + 1):
            try:
                resp = session.get(url, timeout=timeout_seconds)
                # Handle transient server errors with retry
                if resp.status_code in (429, 500, 502, 503, 504):
                    raise requests.HTTPError(
                        f"HTTP {resp.status_code}: {resp.text[:300]}",
                        response=resp,
                    )
                resp.raise_for_status()
                bundle = resp.json()
                break
            except Exception as e:
                last_err = e
                if attempt < retries:
                    time.sleep(retry_backoff * (2 ** attempt))
                    continue
                raise

        if not isinstance(bundle, dict) or bundle.get("resourceType") != "Bundle":
            raise ValueError("Response is not a FHIR Bundle (resourceType != 'Bundle').")

        page_ids = extract_patient_ids(bundle)
        if page_ids:
            if limit is not None:
                remaining = limit - len(collected)
                collected.extend(page_ids[: max(0, remaining)])
            else:
                collected.extend(page_ids)

        next_url = find_next_link(bundle)
        if next_url:
            url = enforce_count_param(next_url, page_size)
        else:
            url = None

    return collected


def write_ids(path: str, ids: list[str]) -> None:
    with open(path, "w", encoding="utf-8") as f:
        for pid in ids:
            f.write(f"{pid}\n")


def parse_args(argv: list[str]) -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Fetch Patient IDs from a FHIR server.")
    p.add_argument("--fhir-server-base", required=True, help="Base URL of the FHIR server (e.g., https://host/fhir)")
    p.add_argument("--count", type=int, default=None, help="Max number of patient ids to write (optional)")
    p.add_argument("--output", required=True, help="Output file path (text file, one patient id per line)")
    p.add_argument("--max", type=int, default=100, help="Page size for _count (optional, default: 100)")

    # Optional tunables (kept as internal defaults; adjust if you like)
    p.add_argument("--timeout", type=float, default=30.0, help="HTTP timeout seconds (default: 30)")
    p.add_argument("--retries", type=int, default=3, help="Retries for transient HTTP errors (default: 3)")
    p.add_argument("--retry-backoff", type=float, default=1.0, help="Base backoff seconds (default: 1.0)")
    return p.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)

    if args.count is not None and args.count <= 0:
        print("--count must be > 0", file=sys.stderr)
        return 2
    if args.max <= 0:
        print("--max must be > 0", file=sys.stderr)
        return 2

    start_url = build_initial_url(args.fhir_server_base, args.max)

    try:
        ids = fetch_all_patient_ids(
            start_url=start_url,
            page_size=args.max,
            limit=args.count,
            timeout_seconds=args.timeout,
            user_agent="fetch-patient-ids/1.0",
            retries=args.retries,
            retry_backoff=args.retry_backoff,
        )
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1

    try:
        write_ids(args.output, ids)
    except Exception as e:
        print(f"Failed to write output file: {e}", file=sys.stderr)
        return 1

    print(f"Wrote {len(ids)} patient ids to {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))