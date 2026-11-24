"""Reads geometry blobs with GDAL and compares them with the expected geometry.

Runs inside the GDAL container started by validate-geopackage-geometry.sh (python3 -I). Input: cases.tsv with the columns
name, tags, blob (hex) and expected WKT, written by the GeoPackageGeometryCasesExport test. Steps:

1. write a throwaway GeoPackage (a GEOMETRY column, SRS 4326) whose geom column holds the blobs exactly as encoded;
2. run `ogrinfo -al` on it and compare the WKT GDAL prints with the expected WKT, token by token, every coordinate
   bit-exact (GDAL is asked to print 17 significant digits, which round-trips a double);
3. read the geometries with the GDAL Python bindings and compare the ISO WKB GDAL writes with the WKB inside the blob.

Exit status 0 only when every case matches. Nothing here is part of the unit tests or a dependency of the projects.
"""
import collections
import os
import re
import sqlite3
import struct
import subprocess
import sys

from osgeo import ogr

ogr.UseExceptions()

GEOGCS = (
    'GEOGCS["WGS 84",DATUM["WGS_1984",SPHEROID["WGS 84",6378137,298.257223563,AUTHORITY["EPSG","7030"]],'
    'AUTHORITY["EPSG","6326"]],PRIMEM["Greenwich",0,AUTHORITY["EPSG","8901"]],'
    'UNIT["degree",0.0174532925199433,AUTHORITY["EPSG","9122"]],AXIS["Latitude",NORTH],AXIS["Longitude",EAST],'
    'AUTHORITY["EPSG","4326"]]'
)
TOKEN = re.compile(r"[A-Za-z]+|[(),]|[-+0-9.eE]+")


def read_cases(path):
    cases = []
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            name, tags, blob_hex, wkt = line.rstrip("\n").split("\t")
            cases.append((name, tags, bytes.fromhex(blob_hex), wkt))
    return cases


def write_geopackage(path, cases):
    if os.path.exists(path):
        os.remove(path)
    db = sqlite3.connect(path)
    db.executescript(
        "PRAGMA application_id = 1196444487; PRAGMA user_version = 10400;"
        "CREATE TABLE gpkg_spatial_ref_sys (srs_name TEXT NOT NULL,srs_id INTEGER NOT NULL PRIMARY KEY,"
        "organization TEXT NOT NULL,organization_coordsys_id INTEGER NOT NULL,definition  TEXT NOT NULL,description TEXT);"
        "CREATE TABLE gpkg_contents (table_name TEXT NOT NULL PRIMARY KEY,data_type TEXT NOT NULL,identifier TEXT UNIQUE,"
        "description TEXT DEFAULT '',last_change DATETIME NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),"
        "min_x DOUBLE, min_y DOUBLE,max_x DOUBLE, max_y DOUBLE,srs_id INTEGER,"
        "CONSTRAINT fk_gc_r_srs_id FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys(srs_id));"
        "CREATE TABLE gpkg_geometry_columns (table_name TEXT NOT NULL,column_name TEXT NOT NULL,"
        "geometry_type_name TEXT NOT NULL,srs_id INTEGER NOT NULL,z TINYINT NOT NULL,m TINYINT NOT NULL,"
        "CONSTRAINT pk_geom_cols PRIMARY KEY (table_name, column_name),CONSTRAINT uk_gc_table_name UNIQUE (table_name),"
        "CONSTRAINT fk_gc_tn FOREIGN KEY (table_name) REFERENCES gpkg_contents(table_name),"
        "CONSTRAINT fk_gc_srs FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys (srs_id));"
        'CREATE TABLE "cases" ("fid" INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, "geom" GEOMETRY, "name" TEXT);'
    )
    db.executemany(
        "INSERT INTO gpkg_spatial_ref_sys VALUES (?,?,?,?,?,?)",
        [
            ("Undefined Cartesian SRS", -1, "NONE", -1, "undefined", "undefined Cartesian coordinate reference system"),
            ("Undefined geographic SRS", 0, "NONE", 0, "undefined", "undefined geographic coordinate reference system"),
            ("WGS 84 geodetic", 4326, "EPSG", 4326, GEOGCS, "longitude/latitude coordinates in decimal degrees on the WGS 84 spheroid"),
        ],
    )
    db.execute(
        "INSERT INTO gpkg_contents (table_name,data_type,identifier,last_change,srs_id) "
        "VALUES ('cases','features','cases','2026-01-01T00:00:00.000Z',4326)"
    )
    db.execute("INSERT INTO gpkg_geometry_columns VALUES ('cases','geom','GEOMETRY',4326,0,0)")
    db.executemany("INSERT INTO cases (geom, name) VALUES (?,?)", [(blob, name) for name, _, blob, _ in cases])
    db.commit()
    db.close()


def tokens(wkt):
    return TOKEN.findall(wkt)


def same_tokens(expected, actual):
    """Words, brackets and commas must be equal; numbers must be the same double, bit for bit."""
    if len(expected) != len(actual):
        return False
    for e, a in zip(expected, actual):
        if re.fullmatch(r"[-+0-9.eE]+", e):
            if not re.fullmatch(r"[-+0-9.eE]+", a) or struct.pack("<d", float(e)) != struct.pack("<d", float(a)):
                return False
        elif e.upper() != a.upper():
            return False
    return True


def gdal_wkt(path):
    """name -> WKT as `ogrinfo -al` prints it."""
    command = [
        "ogrinfo", "-al", "--config", "OGR_WKT_PRECISION", "17", "--config", "OGR_WKT_ROUND", "NO", path,
    ]
    text = subprocess.run(command, check=True, stdout=subprocess.PIPE, universal_newlines=True).stdout
    result, name = {}, None
    for line in text.splitlines():
        stripped = line.strip()
        if line.startswith("OGRFeature("):
            name = None
        elif stripped.startswith("name (String) = "):
            name = stripped[len("name (String) = "):]
        elif name is not None and re.match(r"(POINT|LINESTRING|MULTI|POLYGON|GEOMETRYCOLLECTION)", stripped):
            result[name] = stripped
    return result


def main(cases_path, work_dir):
    cases = read_cases(cases_path)
    gpkg = os.path.join(work_dir, "cases.gpkg")
    write_geopackage(gpkg, cases)

    wkt_by_name = gdal_wkt(gpkg)
    wkt_failures = []
    for name, _, _, expected in cases:
        actual = wkt_by_name.get(name)
        if actual is None or not same_tokens(tokens(expected), tokens(actual)):
            wkt_failures.append((name, expected[:120], (actual or "<missing>")[:120]))

    # The independent ISO WKB of GDAL must equal the WKB inside the blob (after the header and the envelope).
    wkb_failures = []
    source = ogr.Open(gpkg)
    layer = source.GetLayer(0)
    by_name = {name: blob for name, _, blob, _ in cases}
    count = 0
    for feature in layer:
        count += 1
        blob = by_name[feature.GetField("name")]
        envelope_bytes = {0: 0, 1: 32, 2: 48, 3: 48, 4: 64}[(blob[3] >> 1) & 7]
        embedded = blob[8 + envelope_bytes:]
        written = bytes(feature.GetGeometryRef().ExportToIsoWkb(ogr.wkbNDR))
        if written != embedded:
            wkb_failures.append(feature.GetField("name"))

    tag_counts = collections.Counter(tag for _, tags, _, _ in cases for tag in tags.split(","))
    print("GDAL %s: %d cases written, %d features read, layer extent %s" % (
        subprocess.run(["gdalinfo", "--version"], stdout=subprocess.PIPE, universal_newlines=True).stdout.strip(),
        len(cases), count, layer.GetExtent()))
    print("tags: " + ", ".join("%s=%d" % kv for kv in sorted(tag_counts.items())))
    print("WKT from ogrinfo equal to the expected WKT (bit-exact numbers): %d/%d" % (len(cases) - len(wkt_failures), len(cases)))
    print("ISO WKB of GDAL equal to the WKB of the blob:                  %d/%d" % (len(cases) - len(wkb_failures), len(cases)))
    for failure in wkt_failures[:10]:
        print("WKT MISMATCH", failure)
    for name in wkb_failures[:10]:
        print("WKB MISMATCH", name)
    return 1 if (wkt_failures or wkb_failures or count != len(cases)) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))
