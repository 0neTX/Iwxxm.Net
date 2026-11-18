using System;
using System.Collections.Generic;
using Iwxxm.Net.Infrastructure.Csv.Rows;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Csv
{
    /// <summary>
    /// <see cref="IIwxxmDocumentVisitor{TResult}"/> that collects flat CSV rows for any <see cref="IwxxmDocument"/>.
    /// </summary>
    internal sealed class CsvRowCollector : IIwxxmDocumentVisitor<IReadOnlyList<CsvRow>>
    {
        private readonly string? _bulletinId;
        private readonly bool _includeWkt;

        public CsvRowCollector(bool includeWkt, string? bulletinId = null)
        {
            _includeWkt = includeWkt;
            _bulletinId = bulletinId;
        }

        public IReadOnlyList<CsvRow> Visit(Metar metar) =>
            Build(rows => AerodromeCsvRowBuilder.AddAerodromeObservation(metar, _bulletinId, _includeWkt, rows));

        public IReadOnlyList<CsvRow> Visit(Speci speci) =>
            Build(rows => AerodromeCsvRowBuilder.AddAerodromeObservation(speci, _bulletinId, _includeWkt, rows));

        public IReadOnlyList<CsvRow> Visit(Taf report) =>
            Build(rows => AerodromeCsvRowBuilder.AddTaf(report, _bulletinId, _includeWkt, rows));

        public IReadOnlyList<CsvRow> Visit(Sigmet report) =>
            Build(rows => EnRouteHazardCsvRowBuilder.AddSigmet(report, _bulletinId, _includeWkt, rows));

        public IReadOnlyList<CsvRow> Visit(Airmet report) =>
            Build(rows => EnRouteHazardCsvRowBuilder.AddAirmet(report, _bulletinId, _includeWkt, rows));

        public IReadOnlyList<CsvRow> Visit(Vona report) =>
            Build(rows => VolcanicCsvRowBuilder.AddVona(report, _bulletinId, _includeWkt, rows));

        public IReadOnlyList<CsvRow> Visit(Qvaci report) =>
            Build(rows => VolcanicCsvRowBuilder.AddQvaci(report, _bulletinId, _includeWkt, rows));

        public IReadOnlyList<CsvRow> Visit(Vaa report) =>
            Build(rows => VolcanicCsvRowBuilder.AddVaa(report, _bulletinId, _includeWkt, rows));

        public IReadOnlyList<CsvRow> Visit(Tca report) =>
            Build(rows => TcaAndSpaceWxCsvRowBuilder.AddTca(report, _bulletinId, _includeWkt, rows));

        public IReadOnlyList<CsvRow> Visit(SpaceWx report) =>
            Build(rows => TcaAndSpaceWxCsvRowBuilder.AddSpaceWx(report, _bulletinId, _includeWkt, rows));

        public IReadOnlyList<CsvRow> Visit(Sigwx report) =>
            Build(rows => SigwxCsvRowBuilder.AddSigwx(report, _bulletinId, _includeWkt, rows));

        public IReadOnlyList<CsvRow> Visit(MeteorologicalBulletin bulletin)
        {
            var childCollector = new CsvRowCollector(_includeWkt, bulletin.BulletinIdentifier);
            var rows = new List<CsvRow>();
            foreach (IwxxmReport child in bulletin.Reports)
            {
                rows.AddRange(child.Accept(childCollector));
            }

            return rows;
        }

        private static IReadOnlyList<CsvRow> Build(Action<List<CsvRow>> addRows)
        {
            var rows = new List<CsvRow>();
            addRows(rows);
            return rows;
        }
    }
}
