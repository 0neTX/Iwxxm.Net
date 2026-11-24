#!/usr/bin/env bash
# Validates the GeoPackage geometry blobs with GDAL, an independent reader. Not part of the unit tests and not a
# dependency of any project: it needs podman and downloads the pinned GDAL image the first time.
#
# What it does:
#   1. runs one test of the GeoPackage test project, inside the image built by `podman build --target test`, that writes
#      every geometry of the samples (plus a few hand-made ones) as a blob with its expected WKT (cases.tsv);
#   2. in the pinned GDAL image, writes a throwaway GeoPackage from those blobs, reads it with `ogrinfo -al` and with the
#      GDAL Python bindings, and compares (see validate-geopackage-geometry.py).
#
# Usage: tools/validate-geopackage-geometry.sh [work-dir]      (default: a new temporary directory)
# Needs the test image first:  podman build --target test -t iwxxm-net-test .
set -euo pipefail

GDAL_IMAGE='ghcr.io/osgeo/gdal:ubuntu-small-3.13.3@sha256:44fee7d4f9be0966851d7b14a0a387216897d8347f9e0ebc4e812f7217bc39d6'
TEST_IMAGE="${TEST_IMAGE:-iwxxm-net-test}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK="${1:-$(mktemp -d)}"
mkdir -p "$WORK"

podman run --rm -v "$WORK:/cases:Z" -e IWXXM_GPKG_GEOMETRY_CASES_DIR=/cases -w /workspace "$TEST_IMAGE" \
  dotnet test tests/Iwxxm.Net.Export.GeoPackage.Tests/Iwxxm.Net.Export.GeoPackage.Tests.csproj \
  --configuration Release --no-build --filter 'FullyQualifiedName~GeoPackageGeometryCasesExport' > "$WORK/export.log"

podman run --rm -v "$WORK:/work:Z" -v "$HERE/validate-geopackage-geometry.py:/validate.py:Z,ro" "$GDAL_IMAGE" \
  sh -c 'python3 -I /validate.py /work/cases.tsv /work &&
         python3 -I /usr/lib/python3/dist-packages/osgeo_utils/samples/validate_gpkg.py --extra --warning-as-error /work/cases.gpkg'
