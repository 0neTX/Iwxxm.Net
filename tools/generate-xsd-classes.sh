#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# Design-time helper: writes an xscgen REFERENCE SCAFFOLD of the SIGWX transport classes.
#
# The real transport model, src/Iwxxm.Net/Infrastructure/Generated/Iwxxm20252.Generated.cs, is HAND-MAINTAINED
# and is not reproduced by any generator. This script never touches it: it writes the raw output of
# XmlSchemaClassGenerator-console (dotnet-xscgen, pinned in .config/dotnet-tools.json) to
# artifacts/xscgen-reference/ (git-ignored) so that a maintainer can compare it by hand after editing the
# legacy schemas. It is not part of the build.
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
# The transport classes follow the hand-written (non-standard) legacy subset; the official WMO schemas under
# Resources/Xsd/Official are used only for validation and must never be edited.
XSD_DIR="${REPO_ROOT}/src/Iwxxm.Net/Resources/Xsd/Legacy"
OUTPUT_DIR="${REPO_ROOT}/artifacts/xscgen-reference"

mkdir -p "${OUTPUT_DIR}"

echo "Restoring local .NET tools from .config/dotnet-tools.json..."
dotnet tool restore

echo "Writing the xscgen reference scaffold to ${OUTPUT_DIR} (the hand-maintained transport model is not modified)..."
dotnet tool run xscgen \
  --namespace "http://icao.int/iwxxm/2025-2=Iwxxm20252.Generated" \
  --namespace "http://www.opengis.net/gml/3.2=Iwxxm20252.Generated" \
  --namespace "http://www.w3.org/1999/xlink=Iwxxm20252.Generated" \
  --output "${OUTPUT_DIR}" \
  --singleFile \
  --internal \
  --nullable \
  --collectionType "System.Collections.Generic.List\`1" \
  "${XSD_DIR}/xlink.xsd" \
  "${XSD_DIR}/gml.xsd" \
  "${XSD_DIR}/iwxxm-sigwx.xsd"

echo "Reference scaffold written to ${OUTPUT_DIR}. Compare it manually with src/Iwxxm.Net/Infrastructure/Generated/Iwxxm20252.Generated.cs."
