#!/usr/bin/env bash
# Builds, tests and packages the Jev Connector external library for upload
# to the ODC Portal. Run this on a machine with normal internet access
# (it needs nuget.org to restore OutSystems.ExternalLibraries.SDK and the
# xunit test packages).
#
# Usage:
#   ./scripts/build-and-package.sh            # cross-platform publish (default)
#   ./scripts/build-and-package.sh linux-x64  # runtime-specific publish
#
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC_PROJECT="$ROOT_DIR/src/OutSystems.JevConnector/OutSystems.JevConnector.csproj"
TEST_PROJECT="$ROOT_DIR/tests/OutSystems.JevConnector.Tests/OutSystems.JevConnector.Tests.csproj"
RUNTIME="${1:-}"

echo "==> Restoring and running unit tests"
dotnet test "$TEST_PROJECT"

echo "==> Publishing external library (Release)"
if [[ -n "$RUNTIME" ]]; then
  dotnet publish "$SRC_PROJECT" -c Release -r "$RUNTIME" --no-self-contained
  PUBLISH_DIR=$(dirname "$SRC_PROJECT")/bin/Release/net10.0/"$RUNTIME"/publish
else
  dotnet publish "$SRC_PROJECT" -c Release --no-self-contained
  PUBLISH_DIR=$(dirname "$SRC_PROJECT")/bin/Release/net10.0/publish
fi

OUT_ZIP="$ROOT_DIR/ExternalLibrary.zip"
rm -f "$OUT_ZIP"
echo "==> Zipping publish output from: $PUBLISH_DIR"

# Windows paths for the PowerShell fallbacks below (no-ops off Windows).
if command -v cygpath >/dev/null 2>&1; then
  WIN_PUBLISH_DIR=$(cygpath -w "$PUBLISH_DIR")
  WIN_OUT_ZIP=$(cygpath -w "$OUT_ZIP")
else
  WIN_PUBLISH_DIR="$PUBLISH_DIR"
  WIN_OUT_ZIP="$OUT_ZIP"
fi

# The assembly carrying the OSInterface attribute must sit at the ROOT of the
# zip, or the ODC Portal rejects the upload (OS-ELG-MODL-05027). Both branches
# below archive the *contents* of the publish directory, never the directory
# itself.
if command -v zip >/dev/null 2>&1; then
  (cd "$PUBLISH_DIR" && zip -r "$OUT_ZIP" .)
elif command -v powershell.exe >/dev/null 2>&1; then
  # Windows without the zip binary (Git Bash): fall back to Compress-Archive.
  powershell.exe -NoProfile -NonInteractive -Command \
    "Compress-Archive -Path '${WIN_PUBLISH_DIR}\*' -DestinationPath '${WIN_OUT_ZIP}' -CompressionLevel Optimal"
else
  echo "ERROR: need either 'zip' or 'powershell.exe' to build $OUT_ZIP" >&2
  exit 1
fi

echo "==> Verifying the assembly sits at the zip root"
if command -v unzip >/dev/null 2>&1; then
  unzip -Z1 "$OUT_ZIP" | grep -qx 'VRG.JevConnector.dll' || {
    echo "ERROR: VRG.JevConnector.dll is not at the root of $OUT_ZIP (OS-ELG-MODL-05027)" >&2
    exit 1
  }
elif command -v powershell.exe >/dev/null 2>&1; then
  powershell.exe -NoProfile -NonInteractive -Command \
    "Add-Type -AssemblyName System.IO.Compression.FileSystem; \$z = [IO.Compression.ZipFile]::OpenRead('${WIN_OUT_ZIP}'); \$ok = \$z.Entries.FullName -contains 'VRG.JevConnector.dll'; \$z.Dispose(); if (-not \$ok) { Write-Error 'VRG.JevConnector.dll is not at the zip root (OS-ELG-MODL-05027)'; exit 1 }"
else
  echo "WARNING: could not verify zip layout (no unzip/powershell available)" >&2
fi

echo ""
echo "Done. Upload $OUT_ZIP to the ODC Portal:"
echo "  Extend your apps with custom code -> Upload external logic"
