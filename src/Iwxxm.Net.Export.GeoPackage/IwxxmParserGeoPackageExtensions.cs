using System;
using System.IO;
using Iwxxm.Net.Export.GeoPackage;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net
{
    /// <summary>
    /// Parse-and-export members of <see cref="IwxxmParser"/> for the GeoPackage export, with the conventions of the CSV
    /// ones (<c>ProcessToCsv</c>, <c>ParseToCsv</c>).
    /// </summary>
    /// <remarks>
    /// <para>The <c>Process*</c> members are extension methods that run the pipeline of the given parser. The
    /// <c>Parse*</c> members are static methods of this class that use <see cref="IwxxmParser.Default"/>: a C# extension
    /// method cannot be added to the static members of a type, so they are called as
    /// <c>IwxxmParserGeoPackageExtensions.ParseToGeoPackage(...)</c>.</para>
    /// <para><b>Error contract.</b> A parse failure is returned as it is (the same <see cref="ValidationError"/> list as
    /// <c>Process</c>), an export failure is a <see cref="GeoPackageErrorCodes.ExportError"/> failure, and the warnings of
    /// the parse are kept on success. Nothing is written when the parse fails. Only argument validation throws, as for the
    /// parser: <see cref="ArgumentNullException"/> for a <c>null</c> parser or stream, <see cref="ArgumentException"/> for a
    /// <c>null</c>, empty or whitespace input file path. The <b>output</b> path is not argument validated: an unusable one
    /// is an export failure. The success value is the full path of the file written.</para>
    /// </remarks>
    public static class IwxxmParserGeoPackageExtensions
    {
        /// <summary>Validates, parses and exports a document from a stream to the GeoPackage file <paramref name="path"/>.</summary>
        /// <param name="parser">The parser that reads the document.</param>
        /// <param name="xmlStream">The XML document.</param>
        /// <param name="path">The GeoPackage file to write; an existing file is replaced.</param>
        /// <param name="options">The export options, or <c>null</c> for <see cref="GeoPackageExportOptions.Default"/>.</param>
        /// <returns>The full path of the file written, or the parse or export failure.</returns>
        public static Result<string> ProcessToGeoPackage(this IwxxmParser parser, Stream xmlStream, string path, GeoPackageExportOptions? options = null)
        {
            if (parser == null)
                throw new ArgumentNullException(nameof(parser));

            return ExportResult(parser.Process(xmlStream), path, options);
        }

        /// <summary>Validates, parses and exports a document from an XML string to the GeoPackage file <paramref name="path"/>.</summary>
        /// <param name="parser">The parser that reads the document.</param>
        /// <param name="xmlContent">The XML text.</param>
        /// <param name="path">The GeoPackage file to write; an existing file is replaced.</param>
        /// <param name="options">The export options, or <c>null</c> for <see cref="GeoPackageExportOptions.Default"/>.</param>
        /// <returns>The full path of the file written, or the parse or export failure.</returns>
        public static Result<string> ProcessToGeoPackage(this IwxxmParser parser, string xmlContent, string path, GeoPackageExportOptions? options = null)
        {
            if (parser == null)
                throw new ArgumentNullException(nameof(parser));

            return ExportResult(parser.Process(xmlContent), path, options);
        }

        /// <summary>Validates, parses and exports an XML file to the GeoPackage file <paramref name="path"/>.</summary>
        /// <param name="parser">The parser that reads the document.</param>
        /// <param name="filePath">The XML file.</param>
        /// <param name="path">The GeoPackage file to write; an existing file is replaced.</param>
        /// <param name="options">The export options, or <c>null</c> for <see cref="GeoPackageExportOptions.Default"/>.</param>
        /// <returns>The full path of the file written, or the parse or export failure.</returns>
        public static Result<string> ProcessFileToGeoPackage(this IwxxmParser parser, string filePath, string path, GeoPackageExportOptions? options = null)
        {
            if (parser == null)
                throw new ArgumentNullException(nameof(parser));

            return ExportResult(parser.ProcessFile(filePath), path, options);
        }

        /// <summary>Parses a stream and exports it to the GeoPackage file <paramref name="path"/> using <see cref="IwxxmParser.Default"/>.</summary>
        /// <param name="xmlStream">The XML document.</param>
        /// <param name="path">The GeoPackage file to write; an existing file is replaced.</param>
        /// <param name="options">The export options, or <c>null</c> for <see cref="GeoPackageExportOptions.Default"/>.</param>
        /// <returns>The full path of the file written, or the parse or export failure.</returns>
        public static Result<string> ParseToGeoPackage(Stream xmlStream, string path, GeoPackageExportOptions? options = null) =>
            IwxxmParser.Default.ProcessToGeoPackage(xmlStream, path, options);

        /// <summary>Parses an XML string and exports it to the GeoPackage file <paramref name="path"/> using <see cref="IwxxmParser.Default"/>.</summary>
        /// <param name="xmlContent">The XML text.</param>
        /// <param name="path">The GeoPackage file to write; an existing file is replaced.</param>
        /// <param name="options">The export options, or <c>null</c> for <see cref="GeoPackageExportOptions.Default"/>.</param>
        /// <returns>The full path of the file written, or the parse or export failure.</returns>
        public static Result<string> ParseToGeoPackage(string xmlContent, string path, GeoPackageExportOptions? options = null) =>
            IwxxmParser.Default.ProcessToGeoPackage(xmlContent, path, options);

        /// <summary>Parses an XML file and exports it to the GeoPackage file <paramref name="path"/> using <see cref="IwxxmParser.Default"/>.</summary>
        /// <param name="filePath">The XML file.</param>
        /// <param name="path">The GeoPackage file to write; an existing file is replaced.</param>
        /// <param name="options">The export options, or <c>null</c> for <see cref="GeoPackageExportOptions.Default"/>.</param>
        /// <returns>The full path of the file written, or the parse or export failure.</returns>
        public static Result<string> ParseFileToGeoPackage(string filePath, string path, GeoPackageExportOptions? options = null) =>
            IwxxmParser.Default.ProcessFileToGeoPackage(filePath, path, options);

        private static Result<string> ExportResult(Result<IwxxmDocument> parsed, string path, GeoPackageExportOptions? options)
        {
            if (parsed.IsFailure)
                return Result<string>.Failure(parsed.Errors);

            Result<string> exported = IwxxmGeoPackageExporter.Default.Export(parsed.Value, path, options);
            return exported.IsFailure
                ? exported
                : Result<string>.Success(exported.Value, parsed.Warnings);
        }
    }
}
