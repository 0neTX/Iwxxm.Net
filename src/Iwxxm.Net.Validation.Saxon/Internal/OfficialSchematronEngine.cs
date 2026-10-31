using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Saxon.Api;

namespace Iwxxm.Net.Validation.Saxon.Internal
{
    /// <summary>
    /// Process-wide, thread-safe holder of the compiled official Schematron.
    /// <para>
    /// On first use the unmodified official <c>iwxxm.sch</c> is compiled once to XSLT with the vendored SchXslt 1.10.1
    /// pipeline (<c>2.0/pipeline-for-svrl.xsl</c>) and the resulting stylesheet is compiled once by SaxonCS-HE into an
    /// immutable <see cref="XsltExecutable"/>. Each validation loads its own <see cref="Xslt30Transformer"/>
    /// (transformers are not thread-safe; executables are).
    /// </para>
    /// <para>
    /// Every URI dereferenced by Saxon (stylesheet modules, <c>document()</c>, <c>doc()</c>, unparsed text) is served by
    /// <see cref="EmbeddedRuleCatalog"/>; anything else is refused. The official RDF code lists are parsed once (on first use) with
    /// hardened reader settings and shared as immutable trees.
    /// </para>
    /// </summary>
    internal sealed class OfficialSchematronEngine
    {
        // A compile failure is not cached (AUD-L6): the next access retries the compilation.
        private static readonly RetryOnFailureLazy<OfficialSchematronEngine> SharedInstance =
            new RetryOnFailureLazy<OfficialSchematronEngine>(() => new OfficialSchematronEngine());

        private readonly ConcurrentDictionary<string, RetryOnFailureLazy<XdmNode>> _codelistTrees =
            new ConcurrentDictionary<string, RetryOnFailureLazy<XdmNode>>(StringComparer.Ordinal);

        private OfficialSchematronEngine()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Saxon-HE capability only: never requests (or needs) a license key.
            Processor = new Processor(false);
            Processor.SetResourceResolver(ResolveOrRefuse, false);

            Executable = CompileOfficialSchematron();
            CompileTime = stopwatch.Elapsed;
        }

        /// <summary>Gets the shared engine, compiling the official Schematron on first access.</summary>
        public static OfficialSchematronEngine Shared => SharedInstance.Value;

        /// <summary>Gets a value indicating whether the shared engine has already been compiled.</summary>
        public static bool IsCompiled => SharedInstance.IsValueCreated;

        public Processor Processor { get; }

        public XsltExecutable Executable { get; }

        /// <summary>Wall-clock time spent compiling <c>iwxxm.sch</c> to an executable.</summary>
        public TimeSpan CompileTime { get; }

        /// <summary>Saxon product edition and version (for diagnostics, e.g. <c>HE 13.1</c>).</summary>
        public string ProductDescription => Processor.Edition + " " + Processor.ProductVersion;

        /// <summary>
        /// Loads a new transformer whose run-time document resolver only serves the embedded catalog.
        /// </summary>
        public Xslt30Transformer CreateTransformer()
        {
            Xslt30Transformer transformer = Executable.Load30();
            transformer.XmlDocumentResolver = ResolveOrRefuse;
            return transformer;
        }

        /// <summary>Resolves a request from the embedded catalog or throws; never returns <c>null</c> (no fallback).</summary>
        internal IResource ResolveOrRefuse(ResourceRequest request)
        {
            Uri? uri = request?.Uri;
            if (uri == null || EmbeddedRuleCatalog.MapToManifestName(uri, out _) == null)
            {
                throw new InvalidOperationException(
                    $"Access to '{uri}' is refused: the official Schematron runs air-gapped and only embedded resources are available.");
            }

            if (uri.AbsoluteUri.EndsWith(".rdf", StringComparison.Ordinal))
            {
                RetryOnFailureLazy<XdmNode> tree = _codelistTrees.GetOrAdd(
                    uri.AbsoluteUri,
                    _ => new RetryOnFailureLazy<XdmNode>(() => BuildHardenedTree(uri)));
                return new NodeResource(tree.Value);
            }

            return new NodeResource(BuildHardenedTree(uri));
        }

        private XsltExecutable CompileOfficialSchematron()
        {
            // Step 1: compile the SchXslt pipeline (ISO Schematron -> XSLT) from embedded modules.
            XsltCompiler pipelineCompiler = Processor.NewXsltCompiler();
            pipelineCompiler.BaseUri = EmbeddedRuleCatalog.PipelineUri;
            pipelineCompiler.StylesheetModuleResolver = ResolveOrRefuse;

            XsltExecutable pipeline;
            using (Stream pipelineStream = EmbeddedRuleCatalog.OpenRequired(EmbeddedRuleCatalog.PipelineUri))
            {
                pipeline = pipelineCompiler.Compile(pipelineStream);
            }

            // Step 2: transform the byte-identical official iwxxm.sch into an XSLT stylesheet.
            XdmNode schematron = BuildHardenedTree(EmbeddedRuleCatalog.SchematronUri);
            Xslt30Transformer schxslt = pipeline.Load30();
            schxslt.XmlDocumentResolver = ResolveOrRefuse;
            var compiled = new XdmDestination();
            schxslt.ApplyTemplates(schematron, compiled);

            // Step 3: compile the generated stylesheet. It is re-parsed from its serialized form so that its static
            // base URI is the rule folder: the official relative document('codes.wmo.int-*.rdf') calls then resolve to
            // the embedded code lists instead of the SchXslt module location.
            XsltCompiler ruleCompiler = Processor.NewXsltCompiler();
            ruleCompiler.BaseUri = EmbeddedRuleCatalog.CompiledStylesheetUri;
            ruleCompiler.StylesheetModuleResolver = ResolveOrRefuse;
            using (var reader = new StringReader(compiled.XdmNode.OuterXml))
            {
                return ruleCompiler.Compile(reader);
            }
        }

        private XdmNode BuildHardenedTree(Uri uri)
        {
            DocumentBuilder builder = Processor.NewDocumentBuilder();
            builder.BaseUri = uri;

            using (Stream stream = EmbeddedRuleCatalog.OpenRequired(uri))
            using (XmlReader reader = XmlReader.Create(stream, HardenedReaderSettings(null), uri.AbsoluteUri))
            {
                return builder.Build(reader);
            }
        }

        /// <summary>
        /// Reader settings for every XML parsed by this package: DTDs prohibited, no resolver, optional quotas.
        /// </summary>
        internal static XmlReaderSettings HardenedReaderSettings(IwxxmParserOptions? options)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = false
            };

            if (options != null)
            {
                settings.MaxCharactersInDocument = options.MaxCharactersInDocument;
                settings.MaxCharactersFromEntities = options.MaxCharactersFromEntities;
            }

            return settings;
        }
    }
}
