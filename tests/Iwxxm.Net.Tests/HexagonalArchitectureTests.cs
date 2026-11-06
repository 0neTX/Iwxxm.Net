using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NetArchTest.Rules;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// IL-level (Mono.Cecil, via NetArchTest) verification of the hexagonal architecture of <c>Iwxxm.Net</c>.
    /// Dependencies are read from the compiled assembly (method bodies, signatures, attributes, generic arguments),
    /// so they cannot be bypassed with fully-qualified names or aliases the way a <c>using</c>-directive grep can.
    /// </summary>
    /// <remarks>
    /// The public API lives in role-based namespaces (<c>Iwxxm.Net</c>, <c>Iwxxm.Net.Reports</c>,
    /// <c>Iwxxm.Net.Extensibility</c>), so layers are identified by <see cref="ArchitectureLayers"/>: internal layer
    /// namespaces plus an explicit catalog of the root-namespace types. <see cref="EveryRootNamespaceType_IsAssignedToALayer"/>
    /// fails when a new root type is added without being classified.
    /// </remarks>
    public sealed class HexagonalArchitectureTests
    {
        private static readonly Assembly CoreAssembly = typeof(IwxxmParser).Assembly;

        [Fact]
        public void CoreAssembly_HasZeroThirdPartyRuntimeDependencies()
        {
            AssemblyName[] referencedAssemblies = CoreAssembly.GetReferencedAssemblies();

            Assert.All(referencedAssemblies, asm =>
            {
                string name = asm.Name ?? string.Empty;
                bool isBcl = name.Equals("netstandard", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
                             name.StartsWith("System", StringComparison.OrdinalIgnoreCase);

                Assert.True(isBcl, $"Unexpected non-BCL runtime dependency detected: '{name}'.");
            });
        }

        [Fact]
        public void EveryRootNamespaceType_IsAssignedToALayer()
        {
            var unclassified = CoreAssembly.GetTypes()
                .Where(t => !t.IsNested && t.Namespace == ArchitectureLayers.RootNamespace)
                .Where(t => ArchitectureLayers.Classify(t.Namespace, t.Name) == Layer.Unclassified)
                .Select(t => t.FullName)
                .ToList();

            Assert.True(unclassified.Count == 0,
                "Root-namespace types must be assigned to a layer in ArchitectureLayers: " + string.Join(", ", unclassified));
        }

        [Fact]
        public void LayerCatalog_CoversEveryLayer()
        {
            foreach (Layer layer in new[] { Layer.Domain, Layer.Application, Layer.Presentation, Layer.Composition, Layer.Infrastructure })
            {
                Assert.True(SelectLayer(layer).GetTypes().Any(), $"No type was classified into layer '{layer}'.");
            }
        }

        [Fact]
        public void DependencyMatcher_UsesNameSegmentBoundaries()
        {
            // Positive control: Application really depends on IwxxmParserOptions ...
            Assert.NotEmpty(SelectLayer(Layer.Application).And().HaveDependencyOn("Iwxxm.Net.IwxxmParserOptions").GetTypes());

            // ... and "Iwxxm.Net.IwxxmParser" must not match "Iwxxm.Net.IwxxmParserOptions" (segment-boundary matching).
            Assert.Empty(SelectLayer(Layer.Application).And().HaveDependencyOn("Iwxxm.Net.IwxxmParser").GetTypes());
        }

        [Fact]
        public void Domain_DependsOnNothingOutsideTheDomain()
        {
            string[] forbidden = ArchitectureLayers.NamesOf(Layer.Application, Layer.Presentation, Layer.Composition, Layer.Infrastructure)
                .Concat(new[] { "System.Xml", "System.IO", "System.Net", "System.Reflection" })
                .ToArray();

            AssertNoDependency(Layer.Domain, forbidden);
        }

        [Fact]
        public void Application_DependsOnlyOnDomain()
        {
            string[] forbidden = ArchitectureLayers.NamesOf(Layer.Presentation, Layer.Composition, Layer.Infrastructure)
                .Concat(new[] { "System.Xml", "System.Net", "System.Reflection" })
                .ToArray();

            AssertNoDependency(Layer.Application, forbidden);
        }

        [Fact]
        public void Presentation_NeverDependsOnInfrastructureOrXmlParsing()
        {
            string[] forbidden = ArchitectureLayers.NamesOf(Layer.Infrastructure)
                .Concat(new[] { "System.Xml" })
                .ToArray();

            AssertNoDependency(Layer.Presentation, forbidden);
        }

        [Fact]
        public void Infrastructure_IsReachedOnlyThroughTheCompositionRoot()
        {
            string[] forbidden = ArchitectureLayers.NamesOf(Layer.Infrastructure);

            foreach (Layer layer in new[] { Layer.Domain, Layer.Application, Layer.Presentation })
            {
                AssertNoDependency(layer, forbidden);
            }
        }

        [Fact]
        public void Infrastructure_DoesNotDependOnFacadesOrCompositionRoot()
        {
            AssertNoDependency(Layer.Infrastructure, ArchitectureLayers.NamesOf(Layer.Presentation, Layer.Composition));
        }

        [Fact]
        public void GeneratedTransportModel_IsNotPublic()
        {
            TestResult result = Types.InAssembly(CoreAssembly)
                .That().ResideInNamespaceStartingWith(ArchitectureLayers.GeneratedNamespace)
                .ShouldNot().BePublic()
                .GetResult();

            Assert.True(Types.InAssembly(CoreAssembly).That().ResideInNamespaceStartingWith(ArchitectureLayers.GeneratedNamespace).GetTypes().Any());
            Assert.True(result.IsSuccessful, "Public generated types: " + FailingNames(result));
        }

        [Fact]
        public void InfrastructureTypes_AreNotPublic()
        {
            TestResult result = SelectLayer(Layer.Infrastructure)
                .Should().NotBePublic()
                .GetResult();

            Assert.True(result.IsSuccessful, "Public Infrastructure types: " + FailingNames(result));
        }

        [Fact]
        public void PublicTypes_LiveOnlyInRoleBasedNamespaces()
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                ArchitectureLayers.RootNamespace,
                ArchitectureLayers.ReportsNamespace,
                ArchitectureLayers.ExtensibilityNamespace
            };

            var offenders = CoreAssembly.GetExportedTypes()
                .Where(t => !allowed.Contains(t.Namespace ?? string.Empty))
                .Select(t => t.FullName)
                .ToList();

            Assert.True(offenders.Count == 0, "Public types outside the documented namespaces: " + string.Join(", ", offenders));
        }

        [Fact]
        public void OutboundPorts_ArePublicInterfaces()
        {
            var ports = CoreAssembly.GetTypes()
                .Where(t => t.Namespace == ArchitectureLayers.ExtensibilityNamespace)
                .ToList();

            Assert.Contains(ports, t => t.Name == "ISchemaValidator");
            Assert.Contains(ports, t => t.Name == "ISchematronValidator");
            Assert.Contains(ports, t => t.Name == "IIwxxmDocumentMapper");
            Assert.Contains(ports, t => t.Name == "ISigwxDocumentMapper");
            Assert.Contains(ports, t => t.Name == "IGeoJsonExporter");

            // The only non-interface type of the namespace is the neutral document context passed to the ports (HEX-01).
            Type context = Assert.Single(ports, t => !t.IsInterface);
            Assert.Equal("IwxxmDocumentContext", context.Name);
            Assert.True(context.IsPublic && context.IsSealed && context.IsClass);
            Assert.All(ports.Where(t => t != context), t => Assert.True(t.IsInterface && t.IsPublic, $"'{t.FullName}' must be a public interface."));
        }

        [Fact]
        public void OutboundPorts_ReceiveTheDocumentContext_NotAStream()
        {
            // HEX-01: the facade owns reading the input; the ports receive the sanitized, already parsed context.
            foreach (string portName in new[] { "ISchemaValidator", "ISchematronValidator", "IIwxxmDocumentMapper", "ISigwxDocumentMapper" })
            {
                Type port = CoreAssembly.GetType(ArchitectureLayers.ExtensibilityNamespace + "." + portName, throwOnError: true)!;
                foreach (MethodInfo method in port.GetMethods())
                {
                    Assert.All(method.GetParameters(), p => Assert.NotEqual(typeof(System.IO.Stream), p.ParameterType));
                    Assert.Contains(method.GetParameters(), p => p.ParameterType.Name == "IwxxmDocumentContext");
                }
            }
        }

        [Fact]
        public void InputXmlReaderSettings_AreCreatedOnlyBySecureXml()
        {
            // HEX-01: a single place builds the reader settings for input documents (embedded resources excluded).
            string[] allowed =
            {
                "Iwxxm.Net.Infrastructure.Xml.SecureXml",
                "Iwxxm.Net.Infrastructure.Validation.XsdValidator",
                "Iwxxm.Net.Infrastructure.Validation.XsltSchematronValidator",
                "Iwxxm.Net.Infrastructure.Validation.CachedRuleDocumentResolver",
                "Iwxxm.Net.Infrastructure.Mapping.CodeLists.OfficialCodeListReader"
            };

            var offenders = Types.InAssembly(CoreAssembly)
                .That().HaveDependencyOn("System.Xml.XmlReaderSettings")
                .GetTypes()
                .Select(t => t.FullName!)
                .Where(name => !allowed.Any(a => name == a || name.StartsWith(a + "+", StringComparison.Ordinal)))
                .ToList();

            Assert.True(offenders.Count == 0, "Input XmlReaderSettings outside SecureXml: " + string.Join(", ", offenders));
        }

        private static void AssertNoDependency(Layer layer, string[] forbidden)
        {
            TestResult result = SelectLayer(layer)
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();

            Assert.True(result.IsSuccessful,
                $"{layer} types with forbidden dependencies ({string.Join(", ", forbidden)}): {FailingNames(result)}");
        }

        private static PredicateList SelectLayer(Layer layer) =>
            Types.InAssembly(CoreAssembly).That().MeetCustomRule(new LayerMembershipRule(layer));

        private static string FailingNames(TestResult result) =>
            result.FailingTypeNames == null ? string.Empty : string.Join(", ", result.FailingTypeNames);

        private sealed class LayerMembershipRule : ICustomRule
        {
            private readonly Layer _layer;

            public LayerMembershipRule(Layer layer)
            {
                _layer = layer;
            }

            public bool MeetsRule(TypeDefinition type)
            {
                TypeDefinition outermost = type;
                while (outermost.DeclaringType != null)
                {
                    outermost = outermost.DeclaringType;
                }

                return ArchitectureLayers.Classify(outermost.Namespace, outermost.Name) == _layer;
            }
        }
    }

    /// <summary>Hexagonal layers of <c>Iwxxm.Net</c>.</summary>
    public enum Layer
    {
        Unclassified,
        Domain,
        Application,
        Presentation,
        Composition,
        Infrastructure,
        CompilerGenerated
    }

    /// <summary>
    /// Layer catalog: maps the role-based public namespaces and the internal layer namespaces onto hexagonal layers.
    /// Keep in sync with the namespace map documented in README.md and CONTRIBUTING.md.
    /// </summary>
    internal static class ArchitectureLayers
    {
        public const string RootNamespace = "Iwxxm.Net";
        public const string ReportsNamespace = "Iwxxm.Net.Reports";
        public const string ExtensibilityNamespace = "Iwxxm.Net.Extensibility";
        public const string ApplicationNamespace = "Iwxxm.Net.Application";
        public const string InfrastructureNamespace = "Iwxxm.Net.Infrastructure";
        public const string GeneratedNamespace = "Iwxxm20252.Generated";

        private static readonly Dictionary<string, Layer> RootTypes = new Dictionary<string, Layer>(StringComparer.Ordinal)
        {
            // Domain (Domain/Common)
            ["Result`1"] = Layer.Domain,
            ["ValidationError"] = Layer.Domain,
            ["ErrorCategory"] = Layer.Domain,
            ["ValidationSeverity"] = Layer.Domain,

            // Application (Application/Options)
            ["IwxxmParserOptions"] = Layer.Application,
            ["GeoJsonExportOptions"] = Layer.Application,

            // Presentation (Presentation)
            ["IwxxmParser"] = Layer.Presentation,
            ["IwxxmSigwxParser"] = Layer.Presentation,
            ["IwxxmGeoJsonExporter"] = Layer.Presentation,

            // Composition root (Composition)
            ["IwxxmParserBuilder"] = Layer.Composition
        };

        public static Layer Classify(string? ns, string name)
        {
            ns ??= string.Empty;

            if (ns.Length == 0 || IsUnder(ns, "System") || IsUnder(ns, "Microsoft") || name.StartsWith("<", StringComparison.Ordinal))
                return Layer.CompilerGenerated;
            if (ns == RootNamespace)
                return RootTypes.TryGetValue(name, out Layer layer) ? layer : Layer.Unclassified;
            if (IsUnder(ns, ReportsNamespace))
                return Layer.Domain;
            if (IsUnder(ns, ApplicationNamespace) || IsUnder(ns, ExtensibilityNamespace))
                return Layer.Application;
            if (IsUnder(ns, InfrastructureNamespace) || IsUnder(ns, GeneratedNamespace))
                return Layer.Infrastructure;

            return Layer.Unclassified;
        }

        /// <summary>Dependency names (namespaces or fully-qualified type names) that identify the given layers.</summary>
        public static string[] NamesOf(params Layer[] layers)
        {
            var names = new List<string>();
            foreach (Layer layer in layers)
            {
                names.AddRange(RootTypes.Where(kv => kv.Value == layer).Select(kv => RootNamespace + "." + kv.Key));
                switch (layer)
                {
                    case Layer.Domain:
                        names.Add(ReportsNamespace);
                        break;
                    case Layer.Application:
                        names.Add(ApplicationNamespace);
                        names.Add(ExtensibilityNamespace);
                        break;
                    case Layer.Infrastructure:
                        names.Add(InfrastructureNamespace);
                        names.Add(GeneratedNamespace);
                        break;
                }
            }

            return names.ToArray();
        }

        private static bool IsUnder(string ns, string prefix) =>
            ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal);
    }
}
