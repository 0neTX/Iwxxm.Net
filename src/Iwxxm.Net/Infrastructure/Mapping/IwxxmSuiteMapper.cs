using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.Mapping.Reports;
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
            : this(new SigwxDocumentMapper(options), options)
        {
        }

        public IwxxmSuiteMapper(ISigwxDocumentMapper sigwxMapper, IwxxmParserOptions? options = null)
        {
            _sigwxMapper = sigwxMapper ?? throw new ArgumentNullException(nameof(sigwxMapper));
            _options = options ?? IwxxmParserOptions.Default;
        }

        public Result<IIwxxmReport> Map(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = _options.MaxCharactersInDocument,
                MaxCharactersFromEntities = _options.MaxCharactersFromEntities,
                XmlResolver = null
            };

            XDocument doc;
            try
            {
                xmlStream.Position = 0;
                using (XmlReader reader = XmlReader.Create(xmlStream, readerSettings))
                {
                    doc = XDocument.Load(reader, LoadOptions.SetLineInfo);
                }
            }
            catch (XmlException ex)
            {
                return Result<IIwxxmReport>.Failure(
                    "XML-MALFORMED",
                    ex.Message,
                    ErrorCategory.MalformedInput);
            }

            XElement? root = doc.Root;
            if (root == null)
            {
                return Result<IIwxxmReport>.Failure(
                    "XML-EMPTY-ROOT",
                    "The XML document does not contain a root element.",
                    ErrorCategory.MalformedInput);
            }

            // Delegate SIGWX/WAFS directly to the dedicated SIGWX mapper (transport model + ACL)
            if (IsSigwxRoot(root.Name))
            {
                xmlStream.Position = 0;
                return IwxxmReportMapperRegistry.Upcast(_sigwxMapper.Map(xmlStream));
            }

            return MapElementToReport(root);
        }

        private Result<IIwxxmReport> MapElementToReport(XElement root)
        {
            if (root.Name == CollectNs + "MeteorologicalBulletin")
            {
                return IwxxmReportMapperRegistry.Upcast(BulletinMapper.Map(root, MapElementToReport));
            }

            if (root.Name.Namespace != IwxxmNs)
            {
                return Result<IIwxxmReport>.Failure(
                    "XML-UNSUPPORTED-NAMESPACE",
                    $"Unsupported root namespace '{root.Name.NamespaceName}' on element '{root.Name.LocalName}'.",
                    ErrorCategory.Deserialization);
            }

            if (IsSigwxRoot(root.Name))
            {
                return MapEmbeddedSigwx(root);
            }

            if (IwxxmReportMapperRegistry.TryGetMapper(root.Name, out Func<XElement, Result<IIwxxmReport>> mapper))
            {
                return mapper(root);
            }

            return Result<IIwxxmReport>.Failure(
                "XML-UNSUPPORTED-REPORT",
                $"Unsupported IWXXM 2025-2 root element '{root.Name.LocalName}'.",
                ErrorCategory.Deserialization);
        }

        /// <summary>Maps a SIGWX report embedded in a bulletin through the SIGWX outbound port.</summary>
        private Result<IIwxxmReport> MapEmbeddedSigwx(XElement root)
        {
            using (var ms = new MemoryStream())
            {
                root.Save(ms);
                ms.Position = 0;
                return IwxxmReportMapperRegistry.Upcast(_sigwxMapper.Map(ms));
            }
        }

        private static bool IsSigwxRoot(XName name) =>
            name == IwxxmNs + "WAFSSignificantWeatherForecast" || name == IwxxmNs + "SIGWXReport";
    }
}
