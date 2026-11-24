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
#
# Notes:
#   - The work dir is printed first. A temporary directory made here is removed when the run succeeds and kept (and its
#     path printed) when it fails; a directory you pass is never removed. cases.tsv and cases.gpkg in it are deleted first.
#   - Both containers run with `--network none`.
#   - The work dir is mounted as it is, without the `:Z` SELinux relabel (which would relabel a directory of yours). On an
#     SELinux host pass a dedicated empty directory and relabel it yourself (`chcon -Rt container_file_t DIR`), or the
#     containers cannot write to it.
#   - `--no-build` runs the code baked into TEST_IMAGE, so rebuild the image after every change to the sources:
#     `podman build --target test -t iwxxm-net-test .`
set -euo pipefail

GDAL_IMAGE='ghcr.io/osgeo/gdal:ubuntu-small-3.13.3@sha256:44fee7d4f9be0966851d7b14a0a387216897d8347f9e0ebc4e812f7217bc39d6'
TEST_IMAGE="${TEST_IMAGE:-iwxxm-net-test}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

OWN_WORK=0
if [ $# -ge 1 ]; then
  WORK="$1"
  mkdir -p "$WORK"
else
  WORK="$(mktemp -d)"
  OWN_WORK=1
fi

on_exit() {
  local status=$?
  if [ "$status" -ne 0 ]; then
    echo "FAILED (exit status $status); the work files are kept in: $WORK" >&2
  elif [ "$OWN_WORK" -eq 1 ]; then
    rm -rf "$WORK"
  fi
}
trap on_exit EXIT

echo "work dir: $WORK"
rm -f "$WORK/cases.tsv" "$WORK/cases.gpkg"

if ! podman run --rm --network none -v "$WORK:/cases" -e IWXXM_GPKG_GEOMETRY_CASES_DIR=/cases -w /workspace "$TEST_IMAGE" \
  dotnet test tests/Iwxxm.Net.Export.GeoPackage.Tests/Iwxxm.Net.Export.GeoPackage.Tests.csproj \
  --configuration Release --no-build --filter 'FullyQualifiedName~GeoPackageGeometryCasesExport' > "$WORK/export.log" 2>&1; then
  echo "The export step failed; last lines of $WORK/export.log:" >&2
  tail -n 40 "$WORK/export.log" >&2
  exit 1
fi

if [ ! -s "$WORK/cases.tsv" ]; then
  echo "The export step produced no cases: $WORK/cases.tsv is missing or empty; last lines of $WORK/export.log:" >&2
  tail -n 40 "$WORK/export.log" >&2
  exit 1
fi

# The validation script is copied next to the cases, so that only the work dir is mounted.
cp "$HERE/validate-geopackage-geometry.py" "$WORK/validate.py"

podman run --rm --network none -v "$WORK:/work" "$GDAL_IMAGE" \
  sh -c 'python3 -I /work/validate.py /work/cases.tsv /work &&
         python3 -I /usr/lib/python3/dist-packages/osgeo_utils/samples/validate_gpkg.py --extra --warning-as-error /work/cases.gpkg'
