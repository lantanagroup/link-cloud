#!/usr/bin/env python3

import os
import sys
import subprocess
import argparse
import json
from typing import List, Optional

def ensure_git_repo() -> str:
    """Ensure we're at repo root and return root directory path."""
    try:
        root_dir = subprocess.check_output(
            ["git", "rev-parse", "--show-toplevel"], 
            stderr=subprocess.DEVNULL
        ).decode().strip()
        os.chdir(root_dir)
        return root_dir
    except subprocess.CalledProcessError:
        print("Error: not inside a git repository.", file=sys.stderr)
        sys.exit(1)

def has_changes(from_ref: str, to_ref: str, paths: List[str]) -> bool:
    """Check if there are changes in specified paths between refs."""
    try:
        output = subprocess.check_output(
            ["git", "diff", "--name-only", from_ref, to_ref, "--"] + paths,
            stderr=subprocess.DEVNULL
        ).decode()
        return bool(output.strip())
    except subprocess.CalledProcessError:
        return False

def print_file_status(from_ref: str, to_ref: str, paths: List[str]) -> None:
    """Print status (A/M/D) of files that changed between refs."""
    try:
        output = subprocess.check_output(
            ["git", "diff", "--name-status", from_ref, to_ref, "--"] + paths,
            stderr=subprocess.DEVNULL
        ).decode()
        lines = [line for line in output.splitlines() if line.strip()]
        if lines:
            print("\n".join(lines))
    except subprocess.CalledProcessError:
        pass

def print_added_removed_lines(from_ref: str, to_ref: str, file_path: str) -> None:
    """Print added/removed lines from the diff, excluding headers."""
    try:
        diff = subprocess.check_output(
            ["git", "diff", "--unified=0", from_ref, to_ref, "--", file_path],
            stderr=subprocess.DEVNULL
        ).decode()
        
        for line in diff.splitlines():
            if line.startswith('+') and not line.startswith('+++'):
                print(f"ADD: {line[1:]}")
            elif line.startswith('-') and not line.startswith('---'):
                print(f"DEL: {line[1:]}")
    except subprocess.CalledProcessError:
        pass

def call_azure_openai(endpoint: str, deployment: str, api_key: str, changes_output: str) -> str:
    """Call Azure OpenAI to analyze and summarize changes."""
    try:
        import requests
    except ImportError:
        print("Error: 'requests' library is required for Azure OpenAI integration. Install it with: pip install requests", file=sys.stderr)
        sys.exit(1)

    url = f"{endpoint.rstrip('/')}/openai/deployments/{deployment}/chat/completions?api-version=2024-08-01-preview"

    headers = {
        "Content-Type": "application/json",
        "api-key": api_key
    }

    system_prompt = """You are an expert at analyzing deployment changes. Review the git diff output and:
1. Identify false positives and exclude them from summarization - changes that don't actually affect database schema, configuration settings, or Kafka topics (e.g., comments, whitespace, code refactoring, documentation).
2. Summarize the ACTUAL changes that matter for deployment:
   - Database schema changes (new tables, columns, migrations)
   - Configuration setting changes (new settings, changed defaults, removed settings)
   - Kafka topic changes (new topics, renamed topics, removed topics)

Provide a concise summary focusing only on what deployment engineers need to know. When summarizing config changes, provide examples of what the configs should look like in an Azure App Config deployment scenario (i.e. 'Some:Property:Name=SomeValue')"""

    payload = {
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": f"Please analyze these deployment changes:\n\n{changes_output}"}
        ],
        "temperature": 0.3,
        "max_tokens": 2000
    }

    response = requests.post(url, headers=headers, json=payload, timeout=60)
    response.raise_for_status()

    result = response.json()
    return result["choices"][0]["message"]["content"]

def main() -> None:
    parser = argparse.ArgumentParser(
        description="List deployment-relevant changes between two git refs",
        formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("from_ref", metavar="FROM", help="Source git reference (commit, branch, tag)")
    parser.add_argument("to_ref", metavar="TO", help="Target git reference (commit, branch, tag)")
    parser.add_argument("--azure-endpoint",
                        default=os.environ.get("AZURE_OPENAI_ENDPOINT"),
                        help="Azure OpenAI endpoint URL (default: $AZURE_OPENAI_ENDPOINT)")
    parser.add_argument("--azure-deployment",
                        default=os.environ.get("AZURE_OPENAI_COMPLETIONS_DEPLOYMENT"),
                        help="Azure OpenAI deployment name (default: $AZURE_OPENAI_COMPLETIONS_DEPLOYMENT)")
    parser.add_argument("--azure-key",
                        default=os.environ.get("AZURE_OPENAI_API_KEY"),
                        help="Azure OpenAI API key (default: $AZURE_OPENAI_API_KEY)")

    args = parser.parse_args()

    # Check if AI summarization is requested
    ai_enabled = all([args.azure_endpoint, args.azure_deployment, args.azure_key])
    if any([args.azure_endpoint, args.azure_deployment, args.azure_key]) and not ai_enabled:
        print("Error: When using Azure OpenAI, all of --azure-endpoint, --azure-deployment, and --azure-key must be provided", file=sys.stderr)
        sys.exit(1)

    from_ref, to_ref = args.from_ref, args.to_ref
    root_dir = ensure_git_repo()

    # Capture output if AI summarization is enabled
    if ai_enabled:
        import io
        output_buffer = io.StringIO()
        original_stdout = sys.stdout
        sys.stdout = output_buffer

    # Check Kafka topics changes
    print("== Kafka Topics Changes (topics.txt) ==")
    if has_changes(from_ref, to_ref, ["topics.txt"]):
        print_added_removed_lines(from_ref, to_ref, "topics.txt")
    else:
        print("No changes")
    print()

    # Discover services
    dotnet_services = []
    java_services = []

    if os.path.isdir("DotNet"):
        for svc in os.listdir("DotNet"):
            svc_path = os.path.join("DotNet", svc)
            if os.path.isdir(svc_path) and os.path.exists(os.path.join(svc_path, "appsettings.json")):
                dotnet_services.append(svc)

    if os.path.isdir("Java"):
        for svc in os.listdir("Java"):
            svc_path = os.path.join("Java", svc)
            if os.path.isdir(svc_path):
                resources_path = os.path.join(svc_path, "src", "main", "resources")
                if os.path.exists(resources_path) and any(
                    f.startswith("application.") for f in os.listdir(resources_path)
                ):
                    java_services.append(svc)

    print("== Per-Service Changes ==")

    def report_service_changes(lang: str, svc_name: str) -> None:
        print(f"Service ({lang}): {svc_name}")
        base = f"{lang}/{svc_name}"
        had_section = False

        # Database migrations
        db_paths = []
        if lang == "DotNet":
            db_paths.append(f"{base}/Migrations")
        else:
            db_paths.extend([
                f"{base}/src/main/resources/database/migrations",
                f"{base}/src/main/resources/db/migration"
            ])

        if any(has_changes(from_ref, to_ref, [p]) for p in db_paths):
            print("- Database changes:")
            for p in db_paths:
                print_file_status(from_ref, to_ref, [p])
            had_section = True

        # Configuration entity files
        cfg_paths = []
        if lang == "DotNet":
            # Check service-specific config folders
            cfg_paths.extend([
                f"{base}/Settings/*.cs",
                f"{base}/Application/Settings/*.cs",
                f"{base}/Application/Config/*.cs",
                f"{base}/Application/Models/Configuration/*.cs",
                f"{base}/Domain/Settings/*.cs"
            ])
        else:
            # Check Java config packages
            cfg_paths.extend([
                f"{base}/src/main/java/**/configs/*Config.java",
                f"{base}/src/main/java/**/config/*Config.java",
                f"{base}/src/main/java/**/*Settings.java",
                f"{base}/src/main/java/**/*Configuration.java"
            ])

        if any(has_changes(from_ref, to_ref, [p]) for p in cfg_paths):
            print("- Config entity changes:")
            for path in cfg_paths:
                files = subprocess.check_output(["git", "diff", "--name-only", from_ref, to_ref, "--", path]).decode().splitlines()
                for file in files:
                    print(f"  > {file}")
                    print_added_removed_lines(from_ref, to_ref, file)
            had_section = True

        if not had_section:
            print("- No DB or config changes")
        print()

    for svc in sorted(dotnet_services):
        report_service_changes("DotNet", svc)

    for svc in sorted(java_services):
        report_service_changes("Java", svc)

    # Check Shared configuration entities
    print("== Shared Module Changes ==")

    shared_cfg_paths = [
        "DotNet/Shared/Settings/*.cs",
        "DotNet/Shared/Application/Models/Configs/*.cs",
        "Java/shared/src/main/java/**/config/*Config.java",
        "Java/shared/src/main/java/**/*Settings.java"
    ]

    if any(has_changes(from_ref, to_ref, [p]) for p in shared_cfg_paths):
        print("- Shared config entity changes:")
        for path in shared_cfg_paths:
            files = subprocess.check_output(["git", "diff", "--name-only", from_ref, to_ref, "--", path]).decode().splitlines()
            for file in files:
                print(f"  > {file}")
                print_added_removed_lines(from_ref, to_ref, file)
    else:
        print("- No shared config changes")
    print()

    print("Done.")

    # If AI summarization is enabled, restore stdout and process with Azure OpenAI
    if ai_enabled:
        sys.stdout = original_stdout
        changes_output = output_buffer.getvalue()

        # Print the original output
        print(changes_output)

        # Get AI summary
        print("\n" + "="*80)
        print("== AI ANALYSIS & SUMMARY ==")
        print("="*80)
        try:
            summary = call_azure_openai(
                args.azure_endpoint,
                args.azure_deployment,
                args.azure_key,
                changes_output
            )
            print(summary)
        except Exception as e:
            print(f"Error calling Azure OpenAI: {e}", file=sys.stderr)
            sys.exit(1)

if __name__ == "__main__":
    main()