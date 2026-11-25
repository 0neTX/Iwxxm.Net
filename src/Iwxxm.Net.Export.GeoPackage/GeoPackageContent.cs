using System;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>The optional parts of a GeoPackage that are written next to the data tables.</summary>
    /// <remarks>
    /// The data tables (<c>iwxxm_points</c>, <c>iwxxm_lines</c>, <c>iwxxm_polygons</c>, <c>iwxxm_attributes</c>,
    /// <c>iwxxm_reports</c> and <c>iwxxm_metadata</c>) and the tables every GeoPackage has are always written; this
    /// enumeration only selects what is added to them. The numeric values are part of the public contract and never
    /// change. More flags may be added in a later version (each one a power of two, and each one on by default only when
    /// the file stays valid for a reader that ignores it), so code must not assume that these are the only members.
    /// </remarks>
    [Flags]
    internal enum GeoPackageContent
    {
        /// <summary>Only the data tables: no optional part is written.</summary>
        None = 0,

        /// <summary>
        /// The <c>gpkg_schema</c> extension: the tables <c>gpkg_extensions</c>, <c>gpkg_data_columns</c> and
        /// <c>gpkg_data_column_constraints</c>, with a short name, a title and a description for every column of the data
        /// tables, and the MIME type <c>application/json</c> for the <c>properties</c> columns.
        /// </summary>
        SchemaExtension = 1,
    }

    /// <summary>The members of <see cref="GeoPackageContent"/> that this version knows.</summary>
    internal static class GeoPackageContentExtensions
    {
        /// <summary>Every flag this version defines, together.</summary>
        internal const GeoPackageContent All = GeoPackageContent.SchemaExtension;

        /// <summary>The content written when none is given: the schema extension.</summary>
        internal const GeoPackageContent Default = GeoPackageContent.SchemaExtension;

        /// <summary><c>true</c> when <paramref name="content"/> sets no bit that is not a defined flag.</summary>
        internal static bool IsDefined(this GeoPackageContent content) => (content & ~All) == 0;
    }
}
