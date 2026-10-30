using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Contract tests proving that the document hierarchy is closed: the IWXXM 2025-2 catalog is fixed, so the root
    /// <see cref="IwxxmDocument"/> has exactly two branches — the abstract <see cref="IwxxmReport"/> (meteorological reports)
    /// and the sealed <see cref="MeteorologicalBulletin"/> envelope — every abstract type of the hierarchy is non-extensible
    /// from outside the core assembly, every concrete type is sealed, and the visitor stays exhaustive.
    /// </summary>
    public sealed class ClosedReportHierarchyTests
    {
        private static readonly Assembly CoreAssembly = typeof(IwxxmDocument).Assembly;

        private static readonly Type[] AbstractTypes =
        {
            typeof(IwxxmDocument),
            typeof(IwxxmReport),
            typeof(AerodromeObservation)
        };

        private static readonly Type[] ExpectedConcreteReports =
        {
            typeof(Metar), typeof(Speci), typeof(Taf), typeof(Sigmet), typeof(Airmet), typeof(Vona),
            typeof(Qvaci), typeof(Vaa), typeof(Tca), typeof(SpaceWx), typeof(Sigwx)
        };

        public static IEnumerable<object[]> AbstractHierarchyTypes() => AbstractTypes.Select(t => new object[] { t });

        [Theory]
        [MemberData(nameof(AbstractHierarchyTypes))]
        public void AbstractTypes_ArePublicAbstractClasses(Type type)
        {
            Assert.True(type.IsClass && type.IsAbstract && type.IsPublic, $"{type.Name} must be a public abstract class.");
        }

        [Theory]
        [MemberData(nameof(AbstractHierarchyTypes))]
        public void AbstractTypes_HaveNoConstructorAccessibleOutsideTheAssembly(Type type)
        {
            ConstructorInfo[] constructors = type
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.NotEmpty(constructors);
            Assert.All(constructors, c => Assert.False(
                c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly,
                $"{type.Name} constructor '{c}' is accessible to external derived types."));
        }

        [Fact]
        public void Root_HasExactlyTwoBranches_ReportsAndTheBulletinEnvelope()
        {
            List<Type> directSubtypes = DocumentSubtypes().Where(t => t.BaseType == typeof(IwxxmDocument)).ToList();

            Assert.Equal(
                new[] { typeof(IwxxmReport), typeof(MeteorologicalBulletin) }.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal),
                directSubtypes.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal));
            Assert.False(typeof(IwxxmReport).IsAssignableFrom(typeof(MeteorologicalBulletin)), "A bulletin is not a report.");
        }

        [Fact]
        public void Bulletin_ExposesNoPerReportFields()
        {
            string[] properties = typeof(MeteorologicalBulletin).GetProperties().Select(p => p.Name).ToArray();

            Assert.DoesNotContain(nameof(IwxxmReport.TranslationFailedTac), properties);
            Assert.DoesNotContain(nameof(IwxxmReport.IsTranslationFailed), properties);
            Assert.DoesNotContain(nameof(IwxxmReport.PermissibleUsageSupplementary), properties);
            Assert.Contains(nameof(MeteorologicalBulletin.BulletinIdentifier), properties);
            Assert.Contains(nameof(MeteorologicalBulletin.Reports), properties);
        }

        [Fact]
        public void EveryConcreteSubtype_IsSealedAndDeclaredInTheCoreAssembly()
        {
            List<Type> concrete = DocumentSubtypes().Where(t => !t.IsAbstract).ToList();

            Assert.Equal(12, concrete.Count);
            Assert.All(DocumentSubtypes(), t => Assert.Same(CoreAssembly, t.Assembly));
            Assert.All(concrete, t => Assert.True(t.IsSealed, $"'{t.FullName}' must be sealed."));
            Assert.All(DocumentSubtypes().Where(t => t.IsAbstract), t => Assert.Contains(t, AbstractTypes));
        }

        [Fact]
        public void Reports_AreTheElevenOperationalAggregates_WithMetarAndSpeciSharingAerodromeObservation()
        {
            List<Type> reports = DocumentSubtypes().Where(t => !t.IsAbstract && typeof(IwxxmReport).IsAssignableFrom(t)).ToList();

            Assert.Equal(
                ExpectedConcreteReports.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal),
                reports.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal));
            Assert.Equal(typeof(AerodromeObservation), typeof(Metar).BaseType);
            Assert.Equal(typeof(AerodromeObservation), typeof(Speci).BaseType);
            Assert.All(reports.Except(new[] { typeof(Metar), typeof(Speci) }), t => Assert.Equal(typeof(IwxxmReport), t.BaseType));
        }

        [Fact]
        public void Visitor_DeclaresExactlyOneVisitOverloadPerConcreteSubtype()
        {
            List<Type> concrete = CoreAssembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(IwxxmDocument).IsAssignableFrom(t))
                .ToList();

            List<Type> visited = typeof(IIwxxmDocumentVisitor<>).GetMethods()
                .Where(m => m.Name == nameof(IIwxxmDocumentVisitor<int>.Visit))
                .Select(m => m.GetParameters().Single().ParameterType)
                .ToList();

            Assert.Equal(visited.Count, visited.Distinct().Count());
            Assert.Equal(
                concrete.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal),
                visited.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal));
        }

        private static List<Type> DocumentSubtypes() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(LoadableTypes)
                .Where(t => t != typeof(IwxxmDocument) && typeof(IwxxmDocument).IsAssignableFrom(t))
                .ToList();

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
