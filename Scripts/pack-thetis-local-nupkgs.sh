#!/usr/bin/env bash
# Pack LantanaGroup.Thetis.Generation.{Abstractions,Engine} 1.0.0 into
# local-thetis-nupkgs/ from a Thetis checkout. GitHub Actions uses this
# because AZURE_ARTIFACTS_PAT is not set on lantanagroup/link-cloud, so
# restore cannot reach Shared_BOTW_Feed.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
THETIS="${THETIS_DIR:-$ROOT/.thetis}"
OUT="${OUTPUT_DIR:-$ROOT/local-thetis-nupkgs}"
VERSION="${THETIS_PACKAGE_VERSION:-1.0.0}"

abstractions="$THETIS/Thetis.Generation.Abstractions/Thetis.Generation.Abstractions.csproj"
engine="$THETIS/Thetis.Generation.Engine/Thetis.Generation.Engine.csproj"

if [[ ! -f "$abstractions" || ! -f "$engine" ]]; then
  echo "Thetis packable projects not found under $THETIS" >&2
  exit 1
fi

mkdir -p "$OUT"
rm -f "$OUT"/*.nupkg "$OUT"/*.snupkg

# Thetis nuget.config also maps LantanaGroup.Thetis.* to Shared_BOTW_Feed.
# Pack only needs nuget.org (Engine's PackageReferences).
nuget_cfg="$(mktemp)"
trap 'rm -f "$nuget_cfg"' EXIT
cat > "$nuget_cfg" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

pack() {
  dotnet pack "$1" \
    --configuration Release \
    --configfile "$nuget_cfg" \
    -p:Version="$VERSION" \
    -p:TargetFrameworks=net8.0 \
    --output "$OUT"
}

pack "$abstractions"
pack "$engine"

shopt -s nullglob
nupkgs=("$OUT"/LantanaGroup.Thetis.Generation.*.nupkg)
if (( ${#nupkgs[@]} < 2 )); then
  echo "Expected Abstractions and Engine nupkgs in $OUT" >&2
  ls -la "$OUT" >&2
  exit 1
fi

echo "Packed Thetis $VERSION:"
ls -la "$OUT"/*.nupkg
