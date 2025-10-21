#!/usr/bin/env bash
set -Eeuo pipefail

# Runs the Adhoc Reporting smoke test:
#   LantanaGroup.Link.Tests.E2ETests.AdhocReportTest.ExecuteAdhocReportTest
#
# Usage:
#   ./Scripts/run-adhoc-reporting-smoke-test.sh
#   ./Scripts/run-adhoc-reporting-smoke-test.sh --logger "console;verbosity=detailed"

ROOT_DIR="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
cd "${ROOT_DIR}"

dotnet test "Tests/BackendE2ETests/BackendE2ETests.csproj" \
  --filter "FullyQualifiedName~AdhocReportTest.ExecuteAdhocReportTest" \
  "$@"
