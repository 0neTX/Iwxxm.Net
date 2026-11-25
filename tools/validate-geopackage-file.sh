#!/usr/bin/env bash
# Validates the GeoPackage files the writer produces with GDAL, an independent reader. Not part of the unit tests and not
# a dependency of any project: it needs podman and downloads the pinned GDAL image the first time.
#
# What it does:
#   1. runs one test of the GeoPackage test project, inside the image built by `podman build --target test`, that writes a
#      real .gpkg file for every sample (the WMO examples and the SIGWX samples, plus one GeoPackage 1.3 file) and
#      expected.tsv with the row count and extent of every table;
#   2. in the pinned GDAL image, for every file: SQLite's integrity_check and foreign_key_check, `ogrinfo -al -so`, the GDAL
#      Python bindings (layers, feature counts, extents, geometry types, properties JSON) and GDAL's own conformance
#      validator, validate_gpkg.py --extra --warning-as-error (see validate-geopackage-file.py).
#
# Usage: tools/validate-geopackage-file.sh [work-dir]      (default: a new temporary directory)
# Needs the test image first:  podman build --target test -t iwxxm-net-test .
#
# Notes:
#   - The work dir is printed first. A temporary directory made here is removed when the run succeeds and kept (and its
#     path printed) when it fails; a directory you pass is never removed. Everything the script writes is in a
#     subdirectory it owns, gpkg-validate/ (the files/ directory with the .gpkg files and expected.tsv, validate.py and
#     export.log); that subdirectory, and nothing else, is deleted first, so a run never validates a file of an earlier
#     run and never touches other content of a directory you pass.
#   - Both containers run with `--network none`, `--cap-drop=all`, `--security-opt no-new-privileges` and `--pids-limit 512`;
#     the GDAL container gets the subdirectory read-only.
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
OWNED="$WORK/gpkg-validate"
rm -rf "$OWNED"
mkdir -p "$OWNED/files"

CONTAINER_LIMITS=(--network none --cap-drop=all --security-opt no-new-privileges --pids-limit 512)

if ! podman run --rm "${CONTAINER_LIMITS[@]}" -v "$OWNED/files:/files" -e IWXXM_GPKG_FILES_DIR=/files -w /workspace "$TEST_IMAGE" \
  dotnet test tests/Iwxxm.Net.Export.GeoPackage.Tests/Iwxxm.Net.Export.GeoPackage.Tests.csproj \
  --configuration Release --no-build --filter 'FullyQualifiedName~GeoPackageFilesExport' > "$OWNED/export.log" 2>&1; then
  echo "The export step failed; last lines of $OWNED/export.log:" >&2
  tail -n 40 "$OWNED/export.log" >&2
  exit 1
fi

if [ ! -s "$OWNED/files/expected.tsv" ] || ! compgen -G "$OWNED/files/*.gpkg" > /dev/null; then
  echo "The export step produced no files: $OWNED/files has no expected.tsv or no .gpkg; last lines of $OWNED/export.log:" >&2
  tail -n 40 "$OWNED/export.log" >&2
  exit 1
fi

# The validation script is copied next to the files, so that only the owned directory is mounted.
cp "$HERE/validate-geopackage-file.py" "$OWNED/validate.py"

podman run --rm "${CONTAINER_LIMITS[@]}" -v "$OWNED:/work:ro" "$GDAL_IMAGE" python3 -I /work/validate.py /work/files
