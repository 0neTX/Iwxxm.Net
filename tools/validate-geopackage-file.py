"""Reads the GeoPackage files the writer produced with GDAL and with its OGC validator.

Runs inside the GDAL container started by validate-geopackage-file.sh (python3 -I). Input: a directory with the .gpkg files
and expected.tsv (file, table, row count, extent), written by the GeoPackageFilesExport test. For every file:

1. SQLite's own checks: integrity_check, foreign_key_check, application_id "GPKG" and the user_version of the file
   (10400, or 10300 for a file named *.v13.gpkg);
2. `ogrinfo -al -so`: it opens the file with the GPKG driver and lists exactly the expected layers with their feature counts;
3. the GDAL Python bindings: the same layers, the feature count GDAL reports and the number of features it iterates, the
   layer extent (exactly the plain min/max of the written coordinates), the geometry type of each layer, a geometry on every
   feature of a geometry layer, a JSON object in the `properties` field and a date-time type for `issue_time_utc`;
4. GDAL's own conformance validator, validate_gpkg.py, with --extra --warning-as-error.

Exit status 0 only when every check of every file passes and there is at least one file. Nothing here is part of the unit
tests or a dependency of the projects.
"""
import collections
import json
import os
import re
import sqlite3
import subprocess
import sys

from osgeo import ogr

ogr.UseExceptions()

VALIDATOR = "/usr/lib/python3/dist-packages/osgeo_utils/samples/validate_gpkg.py"
# The tables whose rows have an issue_time_utc column (all but the metadata table).
TABLES_WITH_ISSUE_TIME = {"iwxxm_points", "iwxxm_lines", "iwxxm_polygons", "iwxxm_attributes", "iwxxm_reports"}
# The tables whose rows have a JSON `properties` column.
TABLES_WITH_PROPERTIES = {"iwxxm_points", "iwxxm_lines", "iwxxm_polygons", "iwxxm_attributes"}
# The columns of iwxxm_reports, in table order (the fid is not a field).
REPORTS_FIELDS = [
    "report_id", "report_type", "report_status", "permissible_usage", "permissible_usage_reason",
    "permissible_usage_supplementary", "issue_time_utc", "bulletin_identifier",
]
GEOMETRY_TYPES = {
    "iwxxm_points": ogr.wkbPoint,
    "iwxxm_lines": ogr.wkbMultiLineString,
    "iwxxm_polygons": ogr.wkbMultiPolygon,
}


def read_expected(path):
    if not os.path.isfile(path):
        sys.exit("expected file not found: %s (the export step of validate-geopackage-file.sh writes it)" % path)
    files = collections.OrderedDict()
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            name, table, count, extent = line.rstrip("\n").split("\t")
            extent = None if extent == "-" else tuple(float(v) for v in extent.split(" "))
            files.setdefault(name, collections.OrderedDict())[table] = (int(count), extent)
    if not files:
        sys.exit("no files in %s: nothing was validated" % path)
    return files


def sqlite_problems(path, name):
    problems = []
    db = sqlite3.connect(path)
    try:
        if db.execute("PRAGMA integrity_check").fetchall() != [("ok",)]:
            problems.append("integrity_check is not ok")
        if db.execute("PRAGMA foreign_key_check").fetchall():
            problems.append("foreign_key_check reports rows")
        if db.execute("PRAGMA application_id").fetchone()[0] != 0x47504B47:
            problems.append("application_id is not GPKG")
        user_version = 10300 if name.endswith(".v13.gpkg") else 10400
        if db.execute("PRAGMA user_version").fetchone()[0] != user_version:
            problems.append("user_version is not %d" % user_version)
    finally:
        db.close()
    return problems


def ogrinfo_problems(path, tables):
    """`ogrinfo -al -so` must succeed and print every expected layer with its feature count."""
    run = subprocess.run(["ogrinfo", "-al", "-so", path], stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True)
    if run.returncode != 0:
        return ["ogrinfo exited with %d: %s" % (run.returncode, run.stderr.strip()[:200])]
    problems = []
    if "using driver `GPKG' successful" not in run.stdout:
        problems.append("ogrinfo did not open the file with the GPKG driver")
    counts = {}
    layer = None
    for line in run.stdout.splitlines():
        match = re.match(r"Layer name: (\S+)", line)
        if match:
            layer = match.group(1)
        match = re.match(r"Feature Count: (\d+)", line)
        if match and layer is not None:
            counts[layer] = int(match.group(1))
    expected = {table: count for table, (count, _) in tables.items()}
    if counts != expected:
        problems.append("ogrinfo layers/counts %r, expected %r" % (counts, expected))
    return problems


def bindings_problems(path, tables):
    problems = []
    source = ogr.Open(path)
    if source is None:
        return ["GDAL could not open the file"]
    if source.GetDriver().GetName() != "GPKG":
        problems.append("driver is %s" % source.GetDriver().GetName())
    names = [source.GetLayer(i).GetName() for i in range(source.GetLayerCount())]
    if sorted(names) != sorted(tables):
        problems.append("layers %r, expected %r" % (sorted(names), sorted(tables)))
    for table, (count, extent) in tables.items():
        layer = source.GetLayerByName(table)
        if layer is None:
            continue
        if layer.GetFeatureCount() != count:
            problems.append("%s: feature count %d, expected %d" % (table, layer.GetFeatureCount(), count))
        has_geometry = table in GEOMETRY_TYPES
        if has_geometry:
            if layer.GetGeomType() != GEOMETRY_TYPES[table]:
                problems.append("%s: geometry type %s" % (table, ogr.GeometryTypeToName(layer.GetGeomType())))
            if layer.GetExtent() != extent:
                problems.append("%s: extent %r, expected %r" % (table, layer.GetExtent(), extent))
        elif layer.GetGeomType() != ogr.wkbNone:
            problems.append("%s: has a geometry type" % table)
        definition = layer.GetLayerDefn()
        if table in TABLES_WITH_ISSUE_TIME:
            field = definition.GetFieldIndex("issue_time_utc")
            if field < 0 or definition.GetFieldDefn(field).GetType() != ogr.OFTDateTime:
                problems.append("%s: issue_time_utc is not a date-time field" % table)
        if table == "iwxxm_reports":
            fields = [definition.GetFieldDefn(i).GetName() for i in range(definition.GetFieldCount())]
            if fields != REPORTS_FIELDS:
                problems.append("%s: columns %r, expected %r" % (table, fields, REPORTS_FIELDS))
        iterated = 0
        for feature in layer:
            iterated += 1
            if has_geometry and (feature.GetGeometryRef() is None or feature.GetGeometryRef().IsEmpty()):
                problems.append("%s: feature %d has no geometry" % (table, feature.GetFID()))
                break
            if table in TABLES_WITH_PROPERTIES:
                try:
                    if not isinstance(json.loads(feature.GetField("properties")), dict):
                        raise ValueError("not an object")
                except ValueError as error:
                    problems.append("%s: feature %d has invalid properties (%s)" % (table, feature.GetFID(), error))
                    break
        if iterated != count:
            problems.append("%s: iterated %d features, expected %d" % (table, iterated, count))
    return problems


def validator_problems(path):
    run = subprocess.run(
        [sys.executable, "-I", VALIDATOR, "--extra", "--warning-as-error", path],
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, universal_newlines=True)
    return [] if run.returncode == 0 else ["validate_gpkg.py exited with %d: %s" % (run.returncode, run.stdout.strip()[:400])]


def file_problems(path, name, tables):
    """All problems of one file. A file one of the readers cannot open is a problem of that file, not the end of the run."""
    problems = []
    checks = (
        ("SQLite", lambda: sqlite_problems(path, name)),
        ("ogrinfo", lambda: ogrinfo_problems(path, tables)),
        ("GDAL bindings", lambda: bindings_problems(path, tables)),
        ("validate_gpkg.py", lambda: validator_problems(path)),
    )
    for label, check in checks:
        try:
            problems += check()
        except (sqlite3.Error, RuntimeError, OSError, ValueError) as error:
            # sqlite3.DatabaseError for a file that is not a database, RuntimeError from GDAL under UseExceptions.
            problems.append("%s could not read the file: %s: %s" % (label, type(error).__name__, str(error)[:200]))
    return problems


def main(directory):
    files = read_expected(os.path.join(directory, "expected.tsv"))
    version = subprocess.run(["gdalinfo", "--version"], stdout=subprocess.PIPE, universal_newlines=True).stdout.strip()
    failures = 0
    features = 0
    for name, tables in files.items():
        path = os.path.join(directory, name)
        if not os.path.isfile(path):
            problems = ["file was not written"]
        else:
            problems = file_problems(path, name, tables)
        features += sum(count for table, (count, _) in tables.items() if table in GEOMETRY_TYPES or table == "iwxxm_attributes")
        print("%-4s %s" % ("FAIL" if problems else "ok", name))
        for problem in problems:
            print("       " + problem)
        failures += 1 if problems else 0
    print("GDAL %s: %d files, %d with problems, %d features checked" % (version, len(files), failures, features))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
