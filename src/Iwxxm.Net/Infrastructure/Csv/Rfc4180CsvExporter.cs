using System;
using System.Collections.Generic;
using System.Text;
using Iwxxm.Net.Application.Ports.Out;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Csv
{
    /// <summary>
    /// Zero-dependency RFC 4180 CSV exporter for all ICAO/WMO IWXXM 2025-2 Domain Aggregate Roots.
    /// </summary>
    internal sealed class Rfc4180CsvExporter : ICsvExporter
    {
        public string Export(IwxxmDocument document, CsvExportOptions options)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            CsvExportOptions effective = options ?? CsvExportOptions.Default;
            IReadOnlyList<CsvRow> rows = document.Accept(new CsvRowCollector(effective.IncludeGeometryWkt));

            var sb = new StringBuilder(Math.Max(512, (rows.Count + 1) * 160));
            var writer = new Rfc4180CsvWriter(sb, effective);

            if (effective.IncludeHeader)
                writer.WriteHeader(CsvColumns.Headers);

            for (int i = 0; i < rows.Count; i++)
                writer.WriteRow(rows[i]);

            return sb.ToString();
        }
    }
}
