#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# Design-Time CLI Script to Autogenerate C# Classes from IWXXM 2025-2 XSDs.
# Uses the local .NET tool manifest (.config/dotnet-tools.json) defining
# XmlSchemaClassGenerator-console (dotnet-xscgen).
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
# The transport classes are generated from the hand-written (non-standard) legacy subset; the official
# WMO schemas under Resources/Xsd/Official are used only for validation and must never be edited.
XSD_DIR="${REPO_ROOT}/src/Iwxxm.Net/Resources/Xsd/Legacy"
OUTPUT_DIR="${REPO_ROOT}/src/Iwxxm.Net/Infrastructure/Generated"

echo "Restoring local .NET tools from .config/dotnet-tools.json..."
dotnet tool restore

echo "Generating C# classes into namespace Iwxxm20252.Generated..."
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

echo "XSD classes successfully generated in ${OUTPUT_DIR}."
