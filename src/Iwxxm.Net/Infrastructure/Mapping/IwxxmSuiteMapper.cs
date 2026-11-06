using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Iwxxm.Net.Application.Ports.Out;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.Mapping.Reports;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Infrastructure.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;

namespace Iwxxm.Net.Infrastructure.Mapping
{
    /// <summary>
    /// Composite Anti-Corruption Layer (ACL) mapper implementing <see cref="IIwxxmDocumentMapper"/> for the full
    /// ICAO/WMO IWXXM 2025-2 report suite and SWIM <c>collect:MeteorologicalBulletin</c> envelopes.
    /// </summary>
    /// <remarks>
    /// The composite only loads the document safely and dispatches on the root element: per-family mappers live in
    /// <c>Infrastructure/Mapping/Reports</c> (looked up in <see cref="IwxxmReportMapperRegistry"/>), SIGWX documents are
    /// delegated to the <see cref="ISigwxDocumentMapper"/> port and bulletins to <see cref="BulletinMapper"/>.
    /// </remarks>
    internal sealed class IwxxmSuiteMapper : IIwxxmDocumentMapper
    {
        private readonly ISigwxDocumentMapper _sigwxMapper;
        private readonly IwxxmParserOptions _options;

        public IwxxmSuiteMapper(IwxxmParserOptions? options = null)
            : this(new SigwxDocumentMapper(), options)
        {
        }

        public IwxxmSuiteMapper(ISigwxDocumentMapper sigwxMapper, IwxxmParserOptions? options = null)
        {
            _sigwxMapper = sigwxMapper ?? throw new ArgumentNullException(nameof(sigwxMapper));
            _options = options ?? IwxxmParserOptions.Default;
        }

        public Result<IwxxmDocument> Map(IwxxmDocumentContext document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            // Legacy unit aliases are reported once per document (AUD-R5): this call owns the collector of the whole mapping
            // (bulletin entries and an embedded SIGWX report included) and attaches its single aggregated warning.
            var aliases = new UomAliasCollector();
            return aliases.Attach(MapDocument(document, aliases));
        }

        private Result<IwxxmDocument> MapDocument(IwxxmDocumentContext document, UomAliasCollector aliases)
        {
            // The document was parsed once by the use case (shared tree); a direct call parses it here.
            XElement root;
            try
            {
                root = SecureXml.Load(document).Root;
            }
            catch (XmlException ex)
            {
                return Result<IwxxmDocument>.Failure(SecureXml.Malformed(ex));
            }
            catch (InvalidOperationException ex)
            {
                return Result<IwxxmDocument>.Failure(SecureXml.NoRootCode, ex.Message, ErrorCategory.MalformedInput);
            }

            // Delegate SIGWX/WAFS directly to the dedicated SIGWX mapper (transport model + ACL)
            if (IsSigwxRoot(root.Name))
            {
                return IwxxmReportMapperRegistry.ToDocument(MapSigwx(document, aliases));
            }

            if (root.Name == BulletinName)
            {
                return IwxxmReportMapperRegistry.ToDocument(BulletinMapper.Map(root, entry => MapElementToReport(entry, aliases)));
            }

            return IwxxmReportMapperRegistry.ToDocument(MapElementToReport(root, aliases));
        }

        /// <summary>
        /// Maps a report root element (a document root or a <c>collect:meteorologicalInformation</c> child). A bulletin is an
        /// envelope, not a report, so a <c>collect:MeteorologicalBulletin</c> nested in another bulletin is rejected.
        /// </summary>
        private Result<IwxxmReport> MapElementToReport(XElement root, UomAliasCollector aliases)
        {
            if (root.Name == BulletinName)
            {
                return Result<IwxxmReport>.Failure(
                    "COLLECT-NESTED-BULLETIN",
                    "A collect:MeteorologicalBulletin cannot contain another collect:MeteorologicalBulletin; its entries must be IWXXM reports.",
                    ErrorCategory.Deserialization,
                    XmlValueReader.GetGmlId(root));
            }

            if (root.Name.Namespace != IwxxmNs)
            {
                return Result<IwxxmReport>.Failure(
                    "XML-UNSUPPORTED-NAMESPACE",
                    $"Unsupported root namespace '{root.Name.NamespaceName}' on element '{root.Name.LocalName}'.",
                    ErrorCategory.Deserialization);
            }

            if (IsSigwxRoot(root.Name))
            {
                // WAFSSignificantWeatherForecast does not derive from the common ReportType: no translation attributes.
                return MapEmbeddedSigwx(root, aliases);
            }

            if (IwxxmReportMapperRegistry.TryGetMapper(root.Name, out Func<XElement, UomAliasCollector, Result<IwxxmReport>> mapper))
            {
                return ReportTranslationReader.Attach(root, mapper(root, aliases));
            }

            return Result<IwxxmReport>.Failure(
                "XML-UNSUPPORTED-REPORT",
                $"Unsupported IWXXM 2025-2 root element '{root.Name.LocalName}'.",
                ErrorCategory.Deserialization);
        }

        /// <summary>
        /// Maps a SIGWX report embedded in a bulletin through the SIGWX outbound port. The port receives a context over the
        /// already parsed element: the built-in mapper reads the element directly and the report is serialized only if a
        /// custom mapper asks for its bytes (AUD-L5).
        /// </summary>
        private Result<IwxxmReport> MapEmbeddedSigwx(XElement root, UomAliasCollector aliases) =>
            IwxxmReportMapperRegistry.Upcast(MapSigwx(SecureXml.ForElement(root, _options), aliases));

        /// <summary>
        /// Maps a SIGWX document through the port. The built-in mapper shares this document's collector so that one aggregated
        /// alias warning results; a custom mapper behind the port does not know the collector and reports its own warnings.
        /// </summary>
        private Result<Sigwx> MapSigwx(IwxxmDocumentContext context, UomAliasCollector aliases) =>
            _sigwxMapper is SigwxDocumentMapper builtIn ? builtIn.Map(context, aliases) : _sigwxMapper.Map(context);

        private static readonly XName BulletinName = CollectNs + "MeteorologicalBulletin";

        private static bool IsSigwxRoot(XName name) =>
            name == IwxxmNs + "WAFSSignificantWeatherForecast" || name == IwxxmNs + "SIGWXReport";
    }
}
