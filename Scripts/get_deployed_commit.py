#!/usr/bin/env python3
"""
get_from_commit.py

Usage:
    python3 scripts/get_deployed_commit.py <environment>

Arguments:
    environment: One of dev-scale | scale-test | scale-qa

Environment variables expected:
    DEV_BASE_URL, TEST_BASE_URL, QA_BASE_URL  (from your Azure DevOps variable group)

Purpose:
    - Determines the correct BASE_URL from the environment.
    - Calls BASE_URL/api/info and extracts the "Commit" field.
    - Prints an Azure DevOps logging command to set the variable 'fromCommit' as an output variable.
"""

import os
import sys
import json
import urllib.request

REPO_COMMIT_ROOT = 'https://github.com/lantanagroup/link-cloud/commit/'

def fail(msg: str):
    print(f"ERROR: {msg}", file=sys.stderr)
    sys.exit(1)


def main():
    # 1. Determine environment or URL (from arg or kube_namespace env var)
    if len(sys.argv) > 1:
        input_value = sys.argv[1].strip()
    else:
        input_value = os.getenv("kube_namespace", "").strip()

    if not input_value:
        fail("No environment or URL provided. Pass as first argument or via kube_namespace environment variable.")

    # 2. Map environment to BASE_URL or use direct URL
    dev_url = os.getenv("DEV_BASE_URL", "")
    test_url = os.getenv("TEST_BASE_URL", "https://test-admin.nhsnlink.org")
    qa_url = os.getenv("QA_BASE_URL", "")

    base_url = ""
    if input_value.startswith("https://"):
        base_url = input_value
        environment = "custom"
    else:
        environment = input_value
        if environment == "dev-scale":
            base_url = dev_url
        elif environment == "scale-test":
            base_url = test_url
        elif environment == "scale-qa":
            base_url = qa_url
        else:
            fail(f"Unknown environment '{environment}'. Expected one of: dev-scale | scale-test | scale-qa, or a direct https:// URL")

    if not base_url:
        fail(f"BASE_URL is empty for environment '{environment}'. "
             f"Ensure DEV_BASE_URL / TEST_BASE_URL / QA_BASE_URL are defined.")

    print(f"Environment: {environment}")
    print(f"BASE_URL: {base_url}")

    # 3. Query /api/info
    info_url = base_url.rstrip("/") + "/api/info"
    print(f"Querying: {info_url}")

    try:
        request = urllib.request.Request(
            info_url,
            headers={'Accept': 'application/json'}
        )
        with urllib.request.urlopen(request) as response:
            body = response.read().decode("utf-8")
    except Exception as e:
        fail(f"Failed to GET {info_url}: {e}")

    print(f"Response: {body}")

    # 4. Parse JSON and extract "Commit" (case-insensitive)
    try:
        data = json.loads(body)
    except json.JSONDecodeError as e:
        fail(f"Invalid JSON response from {info_url}: {e}")

    # Handle array response by taking first element
    if isinstance(data, list) and len(data) > 0:
        data = data[0]

    commit = data.get("Commit") or data.get("commit") or ""
    if not commit:
        fail("Could not find 'Commit' in /api/info response.")

    # If we got a short hash, try to match it with the full hash from git log
    if len(commit) < 40:  # Full SHA-1 hash is 40 characters
        print(f"Attempting to translate short commit hash {commit} to full commit hash")
        try:
            request = urllib.request.Request(
                f"{REPO_COMMIT_ROOT}{commit}",
                headers={'Accept': 'application/json'}
            )
            with urllib.request.urlopen(request) as response:
                full_commit_data = json.loads(response.read().decode("utf-8"))
                commit = full_commit_data.get('payload', {}).get('commit', {}).get("sha2", commit)
        except Exception as e:
            print(f"Warning: Could not resolve full commit hash: {e}", file=sys.stderr)

    print(f"FromCommit: {commit}")

    # 5. Emit Azure DevOps logging command
    # This will make the variable available as an output variable in the step that runs this script.
    print(f"##vso[task.setvariable variable=FromCommit]{commit}")


if __name__ == "__main__":
    main()
