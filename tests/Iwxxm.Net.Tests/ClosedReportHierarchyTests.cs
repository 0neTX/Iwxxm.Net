using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Contract tests proving that the <see cref="IwxxmReport"/> hierarchy is closed: the IWXXM 2025-2 catalog is fixed,
    /// so only the sealed report aggregates of the core assembly can derive from the base, and the visitor stays exhaustive.
    /// </summary>
    public sealed class ClosedReportHierarchyTests
    {
        private static readonly Assembly CoreAssembly = typeof(IwxxmReport).Assembly;

        [Fact]
        public void Base_IsPublicAbstractClass()
        {
            Type type = typeof(IwxxmReport);

            Assert.True(type.IsClass && type.IsAbstract && type.IsPublic, "IwxxmReport must be a public abstract class.");
        }

        [Fact]
        public void Base_HasNoConstructorAccessibleOutsideTheAssembly()
        {
            ConstructorInfo[] constructors = typeof(IwxxmReport)
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.NotEmpty(constructors);
            Assert.All(constructors, c => Assert.False(
                c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly,
                $"IwxxmReport constructor '{c}' is accessible to external derived types."));
        }

        [Fact]
        public void EverySubtype_IsSealedAndDeclaredInTheCoreAssembly()
        {
            List<Type> subtypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(LoadableTypes)
                .Where(t => t != typeof(IwxxmReport) && typeof(IwxxmReport).IsAssignableFrom(t))
                .ToList();

            Assert.Equal(11, subtypes.Count);
            Assert.All(subtypes, t =>
            {
                Assert.Same(CoreAssembly, t.Assembly);
                Assert.True(t.IsSealed, $"'{t.FullName}' must be sealed.");
                Assert.Equal(typeof(IwxxmReport), t.BaseType);
            });
        }

        [Fact]
        public void Visitor_DeclaresExactlyOneVisitOverloadPerConcreteSubtype()
        {
            List<Type> subtypes = CoreAssembly.GetTypes()
                .Where(t => t != typeof(IwxxmReport) && typeof(IwxxmReport).IsAssignableFrom(t))
                .ToList();

            List<Type> visited = typeof(IIwxxmReportVisitor<>).GetMethods()
                .Where(m => m.Name == nameof(IIwxxmReportVisitor<int>.Visit))
                .Select(m => m.GetParameters().Single().ParameterType)
                .ToList();

            Assert.Equal(visited.Count, visited.Distinct().Count());
            Assert.Equal(
                subtypes.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal),
                visited.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal));
        }

        private static IEnumerable<Type> LoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null)!;
            }
        }
    }
}
