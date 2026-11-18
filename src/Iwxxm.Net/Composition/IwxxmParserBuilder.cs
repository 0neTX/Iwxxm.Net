using System;
using Iwxxm.Net.Application.Ports.Out;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.Csv;
using Iwxxm.Net.Infrastructure.GeoJson;
using Iwxxm.Net.Infrastructure.Mapping;
using Iwxxm.Net.Infrastructure.Validation;
using Iwxxm.Net.Infrastructure.Xml;

namespace Iwxxm.Net
{
    /// <summary>
    /// Composition root of <c>Iwxxm.Net</c>: wires the driven adapters (schema validator, business-rule validator,
    /// document mappers, GeoJSON exporter) to the facades and returns ready-to-use facades.
    /// The business-rule (Schematron) validator can be replaced through <see cref="ISchematronValidator"/> in <see cref="Iwxxm.Net.Extensibility"/>; the other adapters are internal.
    /// </summary>
    /// <remarks>
    /// <para>Defaults (when no adapter is configured):</para>
    /// <list type="bullet">
    /// <item>Schema validation: embedded, byte-identical official WMO IWXXM 2025-2 / COLLECT XSDs (full suite for
    /// <see cref="Build"/>; SIGWX roots only for <see cref="BuildSigwxParser"/>).</item>
    /// <item>Business rules: the built-in XSLT 1.0 subset of the official Schematron (11 of 165 rule IDs plus official
    /// WMO code-list membership checks). Use <see cref="UseSchematronValidator(ISchematronValidator)"/> (for example the
    /// optional <c>Iwxxm.Net.Validation.Saxon</c> package, which runs the complete official <c>iwxxm.sch</c>) or
    /// <see cref="WithoutSchematron"/>.</item>
    /// <item>Mapping and GeoJSON: built-in Anti-Corruption Layer mappers and RFC 7946 exporter.</item>
    /// </list>
    /// <para>The builder is not thread-safe; the facades it builds are immutable and thread-safe and are intended to be
    /// registered as singletons.</para>
    /// </remarks>
    public sealed class IwxxmParserBuilder
    {
        private IwxxmParserOptions _options = IwxxmParserOptions.Default;
        private ISchemaValidator? _schemaValidator;
        private Func<IwxxmParserOptions, ISchematronValidator>? _schematronFactory;
        private IIwxxmDocumentMapper? _documentMapper;
        private ISigwxDocumentMapper? _sigwxDocumentMapper;
        private IGeoJsonExporter? _geoJsonExporter;
        private ICsvExporter? _csvExporter;

        /// <summary>Gets the parser options that will be passed to the built-in adapters.</summary>
        public IwxxmParserOptions Options => _options;

        /// <summary>Sets the security and memory-bound options (size and character quotas).</summary>
        public IwxxmParserBuilder WithOptions(IwxxmParserOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            return this;
        }

        /// <summary>
        /// Replaces the structural schema validator for both the full-suite and the SIGWX pipelines.
        /// The adapter is responsible for rejecting root elements it does not support.
        /// </summary>
        internal IwxxmParserBuilder UseSchemaValidator(ISchemaValidator validator)
        {
            _schemaValidator = validator ?? throw new ArgumentNullException(nameof(validator));
            return this;
        }

        /// <summary>Replaces the business-rule (Schematron) validator for both pipelines.</summary>
        public IwxxmParserBuilder UseSchematronValidator(ISchematronValidator validator)
        {
            if (validator == null)
                throw new ArgumentNullException(nameof(validator));

            _schematronFactory = _ => validator;
            return this;
        }

        /// <summary>
        /// Replaces the business-rule (Schematron) validator with an adapter created from the final
        /// <see cref="IwxxmParserOptions"/> when a facade is built.
        /// </summary>
        public IwxxmParserBuilder UseSchematronValidator(Func<IwxxmParserOptions, ISchematronValidator> factory)
        {
            _schematronFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        /// <summary>
        /// Disables business-rule validation: documents are only checked against the schemas before mapping.
        /// </summary>
        public IwxxmParserBuilder WithoutSchematron()
        {
            _schematronFactory = _ => DisabledSchematronValidator.Instance;
            return this;
        }

        /// <summary>Replaces the mapper used by <see cref="IwxxmParser"/> to build Domain Aggregate Roots.</summary>
        internal IwxxmParserBuilder UseDocumentMapper(IIwxxmDocumentMapper mapper)
        {
            _documentMapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            return this;
        }

        /// <summary>Replaces the mapper used by <see cref="IwxxmSigwxParser"/> (and by the default full-suite mapper for SIGWX roots).</summary>
        internal IwxxmParserBuilder UseSigwxDocumentMapper(ISigwxDocumentMapper mapper)
        {
            _sigwxDocumentMapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            return this;
        }

        /// <summary>Replaces the GeoJSON exporter.</summary>
        internal IwxxmParserBuilder UseGeoJsonExporter(IGeoJsonExporter exporter)
        {
            _geoJsonExporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            return this;
        }

        /// <summary>Replaces the CSV exporter.</summary>
        internal IwxxmParserBuilder UseCsvExporter(ICsvExporter exporter)
        {
            _csvExporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            return this;
        }

        /// <summary>Builds the full-suite parser facade (all IWXXM 2025-2 reports and COLLECT bulletins).</summary>
        public IwxxmParser Build()
        {
            ISigwxDocumentMapper sigwxMapper = _sigwxDocumentMapper ?? new SigwxDocumentMapper();
            return new IwxxmParser(
                schemaValidator: _schemaValidator ?? XsdValidator.CreateFullSuiteValidator(_options),
                schematronValidator: CreateSchematronValidator(),
                documentMapper: _documentMapper ?? new IwxxmSuiteMapper(sigwxMapper, _options),
                geoJsonExporter: _geoJsonExporter ?? new Rfc7946GeoJsonExporter(),
                csvExporter: _csvExporter ?? new Rfc4180CsvExporter(),
                parser: SecureXml.Parser,
                options: _options);
        }

        /// <summary>Builds the SIGWX parser facade (<c>iwxxm:WAFSSignificantWeatherForecast</c> and <c>iwxxm:SIGWXReport</c>).</summary>
        public IwxxmSigwxParser BuildSigwxParser()
        {
            return new IwxxmSigwxParser(
                schemaValidator: _schemaValidator ?? new XsdValidator(useFullSuite: false, options: _options),
                schematronValidator: CreateSchematronValidator(),
                documentMapper: _sigwxDocumentMapper ?? new SigwxDocumentMapper(),
                parser: SecureXml.Parser,
                options: _options);
        }

        /// <summary>Builds the GeoJSON exporter facade.</summary>
        public IwxxmGeoJsonExporter BuildGeoJsonExporter() => new IwxxmGeoJsonExporter(_geoJsonExporter ?? new Rfc7946GeoJsonExporter());

        /// <summary>Builds the CSV exporter facade.</summary>
        public IwxxmCsvExporter BuildCsvExporter() => new IwxxmCsvExporter(_csvExporter ?? new Rfc4180CsvExporter());

        private ISchematronValidator CreateSchematronValidator()
        {
            if (_schematronFactory == null)
                return new XsltSchematronValidator(_options);

            return _schematronFactory(_options)
                   ?? throw new InvalidOperationException("The configured Schematron validator factory returned null.");
        }
    }
}
