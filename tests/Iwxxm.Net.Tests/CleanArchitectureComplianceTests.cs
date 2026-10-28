using System;
using System.Linq;
using System.Reflection;
using Iwxxm.Net.Presentation;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Architectural verification tests enforcing Clean Architecture dependency rules
    /// and encapsulation across Domain, Application, Infrastructure, and Presentation layers.
    /// </summary>
    public sealed class CleanArchitectureComplianceTests
    {
        private static readonly Assembly CoreAssembly = typeof(IwxxmSigwxParser).Assembly;

        [Fact]
        public void CoreAssembly_HasZeroThirdPartyRuntimeDependencies()
        {
            // Assert that the compiled assembly only references standard .NET BCL assemblies
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
        public void DomainLayer_DoesNotDependOnOuterLayersOrXmlInfrastructure()
        {
            var domainTypes = CoreAssembly.GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("Iwxxm.Net.Domain", StringComparison.Ordinal))
                .ToList();

            Assert.NotEmpty(domainTypes);

            foreach (Type domainType in domainTypes)
            {
                // Verify properties, fields, and method signatures do not expose or depend on outer namespaces
                var referencedTypes = domainType
                    .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .SelectMany(GetMemberReferencedTypes)
                    .Where(t => t.Namespace != null)
                    .ToList();

                foreach (Type dep in referencedTypes)
                {
                    Assert.False(dep.Namespace!.StartsWith("Iwxxm.Net.Application", StringComparison.Ordinal),
                        $"Domain type '{domainType.FullName}' illegally depends on Application type '{dep.FullName}'.");
                    Assert.False(dep.Namespace!.StartsWith("Iwxxm.Net.Infrastructure", StringComparison.Ordinal),
                        $"Domain type '{domainType.FullName}' illegally depends on Infrastructure type '{dep.FullName}'.");
                    Assert.False(dep.Namespace!.StartsWith("Iwxxm.Net.Presentation", StringComparison.Ordinal),
                        $"Domain type '{domainType.FullName}' illegally depends on Presentation type '{dep.FullName}'.");
                    Assert.False(dep.Namespace!.StartsWith("Iwxxm20252.Generated", StringComparison.Ordinal),
                        $"Domain type '{domainType.FullName}' illegally depends on Generated XSD type '{dep.FullName}'.");
                    Assert.False(dep.Namespace!.StartsWith("System.Xml", StringComparison.Ordinal),
                        $"Domain type '{domainType.FullName}' illegally depends on System.Xml type '{dep.FullName}'.");
                }
            }
        }

        [Fact]
        public void ApplicationLayer_DoesNotDependOnInfrastructureOrPresentationOrSystemXml()
        {
            var applicationTypes = CoreAssembly.GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("Iwxxm.Net.Application", StringComparison.Ordinal))
                .ToList();

            Assert.NotEmpty(applicationTypes);

            foreach (Type appType in applicationTypes)
            {
                var referencedTypes = appType
                    .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .SelectMany(GetMemberReferencedTypes)
                    .Where(t => t.Namespace != null)
                    .ToList();

                foreach (Type dep in referencedTypes)
                {
                    Assert.False(dep.Namespace!.StartsWith("Iwxxm.Net.Infrastructure", StringComparison.Ordinal),
                        $"Application type '{appType.FullName}' illegally depends on Infrastructure type '{dep.FullName}'.");
                    Assert.False(dep.Namespace!.StartsWith("Iwxxm.Net.Presentation", StringComparison.Ordinal),
                        $"Application type '{appType.FullName}' illegally depends on Presentation type '{dep.FullName}'.");
                    Assert.False(dep.Namespace!.StartsWith("Iwxxm20252.Generated", StringComparison.Ordinal),
                        $"Application type '{appType.FullName}' illegally depends on Generated XSD type '{dep.FullName}'.");
                    Assert.False(dep.Namespace!.StartsWith("System.Xml", StringComparison.Ordinal),
                        $"Application type '{appType.FullName}' illegally depends on System.Xml type '{dep.FullName}'.");
                }
            }
        }

        [Fact]
        public void InfrastructureLayer_TypesAreInternalAndNotExposedPublicly()
        {
            var infraTypes = CoreAssembly.GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("Iwxxm.Net.Infrastructure", StringComparison.Ordinal))
                .ToList();

            Assert.NotEmpty(infraTypes);
            Assert.All(infraTypes, t => Assert.False(t.IsPublic, $"Infrastructure type '{t.FullName}' should be internal."));
        }

        private static Type[] GetMemberReferencedTypes(MemberInfo member)
        {
            return member switch
            {
                FieldInfo f => new[] { f.FieldType },
                PropertyInfo p => new[] { p.PropertyType },
                MethodInfo m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType).ToArray(),
                ConstructorInfo c => c.GetParameters().Select(p => p.ParameterType).ToArray(),
                _ => Array.Empty<Type>()
            };
        }
    }
}
