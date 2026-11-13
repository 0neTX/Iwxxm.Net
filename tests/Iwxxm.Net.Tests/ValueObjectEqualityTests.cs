using System;
using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Property-style verification of the structural equality of the reference-type value objects (DDD-09, AUD-L10):
    /// two instances built independently from the same components are equal (<c>Equals</c>, <c>==</c>) and have the same
    /// hash code; equality is reflexive, symmetric and exact (no tolerance), collections are compared by content and in
    /// order, and changing one component breaks equality. Inputs come from seeded generators, so runs are deterministic.
    /// </summary>
    public sealed class ValueObjectEqualityTests
    {
        private const int Samples = 300;
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public static IEnumerable<object[]> Generators()
        {
            yield return Case("SurfaceWind", Wind);
            yield return Case("RunwayVisualRange", Rvr);
            yield return Case("AerodromeCloudLayer", CloudLayer);
            yield return Case("AerodromeForecastGroup", ForecastGroup);
            yield return Case("EnRoutePhenomenonVolume", Volume);
            yield return Case("JetStreamCorePoint", CorePoint);
            yield return Case("JetStream", Jet);
            yield return Case("VolcanicAshConcentrationCloud", AshCloud);
            yield return Case("TropicalCycloneTrackPoint", TrackPoint);
            yield return Case("SpaceWeatherRegion", SpaceWeatherRegionCase);
            yield return Case("SpaceWeatherIntensityAndRegion", SpaceWeatherIntensityAndRegionCase);
            yield return Case("SpaceWeatherAnalysis", SpaceWeatherAnalysisCase);
            yield return Case("AirmetEvolvingCondition", AirmetCondition);
            yield return Case("VolcanicAshConditions", AshConditions);
            yield return Case("CodeListValue<SigmetPhenomenon>", PhenomenonCode);
            yield return Case("AerodromeWeather", Weather);
        }

        [Theory]
        [MemberData(nameof(Generators))]
        public void IndependentlyBuiltEqualValues_AreEqualWithTheSameHashCode(string type, Func<Random, object> generate)
        {
            for (int seed = 0; seed < Samples; seed++)
            {
                object a = generate(new Random(seed));
                object b = generate(new Random(seed));

                Assert.False(ReferenceEquals(a, b), type);
                Assert.True(a.Equals(b), $"{type} seed {seed}: equal components must give equal values.");
                Assert.True(b.Equals(a), $"{type} seed {seed}: equality must be symmetric.");
                Assert.True(a.Equals(a), $"{type} seed {seed}: equality must be reflexive.");
                Assert.False(a.Equals(null), type);
                Assert.Equal(a.GetHashCode(), b.GetHashCode());
                Assert.True(InvokeEqualityOperator(a, b), $"{type} seed {seed}: operator == must agree with Equals.");
            }
        }

        [Theory]
        [MemberData(nameof(Generators))]
        public void EqualValues_AlwaysHaveEqualHashCodes_AndHashSetsDeduplicateThem(string type, Func<Random, object> generate)
        {
            var values = Enumerable.Range(0, Samples).Select(seed => generate(new Random(seed % 50))).ToList();

            foreach (object a in values)
            {
                foreach (object b in values.Where(a.Equals))
                {
                    Assert.True(a.GetHashCode() == b.GetHashCode(), $"{type}: equal values must have equal hash codes.");
                }
            }

            Assert.Equal(values.Distinct().Count(), new HashSet<object>(values).Count);
            Assert.True(new HashSet<object>(values).Count <= 50, type);
        }

        [Fact]
        public void PositiveAndNegativeZero_AreEqual_WithTheSameHashCode()
        {
            var a = new SurfaceWind(false, 0.0, null);
            var b = new SurfaceWind(false, -0.0, null);
            var rvrA = new RunwayVisualRange("27", 0.0);
            var rvrB = new RunwayVisualRange("27", -0.0);

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.Equal(rvrA, rvrB);
            Assert.Equal(rvrA.GetHashCode(), rvrB.GetHashCode());
        }

        [Fact]
        public void Comparison_IsExact_WithoutTolerance()
        {
            var a = new SurfaceWind(false, 240.0, new Speed(4.0, SpeedUnit.MetersPerSecond));
            var b = new SurfaceWind(false, 240.0000000001, new Speed(4.0, SpeedUnit.MetersPerSecond));
            var c = new SurfaceWind(false, 240.0, new Speed(4.0, SpeedUnit.Knots));

            Assert.NotEqual(a, b);
            Assert.NotEqual(a, c);
            Assert.True(a != b);
        }

        [Fact]
        public void Collections_AreComparedByContent_AndOrder()
        {
            var layer1 = new AerodromeCloudLayer(Amount(AerodromeCloudAmount.Few, "FEW"), new Length(1000, LengthUnit.Feet));
            var layer2 = new AerodromeCloudLayer(Amount(AerodromeCloudAmount.Broken, "BKN"), new Length(2000, LengthUnit.Feet));
            var fg = new AerodromeWeather("FG", isKnown: true);

            AerodromeForecastGroup Group(params AerodromeCloudLayer[] layers) =>
                new AerodromeForecastGroup("g", "BECOMING", false, Epoch, Epoch.AddHours(1), 800, null, null, new[] { fg }, new AerodromeCloud(layers));

            Assert.Equal(Group(layer1, layer2), Group(
                new AerodromeCloudLayer(Amount(AerodromeCloudAmount.Few, "FEW"), new Length(1000, LengthUnit.Feet)),
                new AerodromeCloudLayer(Amount(AerodromeCloudAmount.Broken, "BKN"), new Length(2000, LengthUnit.Feet))));
            Assert.NotEqual(Group(layer1, layer2), Group(layer2, layer1));
            Assert.NotEqual(Group(layer1, layer2), Group(layer1));
        }

        [Fact]
        public void JetStream_ComparesItsCorePoints()
        {
            var location = new GeoCoordinate(10, 20);
            VerticalLimit level = VerticalLimit.Create(350, VerticalUnit.FlightLevel, VerticalReference.Std).Value;
            var point = new JetStreamCorePoint(location, level, new Speed(120, SpeedUnit.Knots));
            var otherPoint = new JetStreamCorePoint(location, level, new Speed(130, SpeedUnit.Knots));
            var axis = new[] { new GeoCoordinate(10, 20), new GeoCoordinate(11, 21) };

            JetStream With(JetStreamCorePoint p) => JetStream.Create("jet", axis, level, new Speed(120, SpeedUnit.Knots), new[] { p }).Value;

            Assert.Equal(With(point), With(new JetStreamCorePoint(location, level, new Speed(120, SpeedUnit.Knots))));
            Assert.NotEqual(With(point), With(otherPoint));
        }

        [Fact]
        public void ChangingOneComponent_BreaksEquality()
        {
            var volume = Volume(new Random(7));
            var changed = new EnRoutePhenomenonVolume(
                volume.Id, volume.Area, volume.LowerLimit, volume.UpperLimit, IntensityChange.Weaken,
                volume.DirectionOfMotionDegrees, volume.SpeedOfMotion, volume.TropicalCycloneCentre);

            Assert.NotEqual(volume.IntensityChange, changed.IntensityChange);
            Assert.NotEqual(volume, changed);
            Assert.True(volume != changed);

            Assert.NotEqual(Amount(AerodromeCloudAmount.Broken, "BKN"), Amount(AerodromeCloudAmount.Unknown, "BKN"));
            Assert.NotEqual(new AerodromeWeather("FG", true), new AerodromeWeather("FG", false));
        }

        [Fact]
        public void NullOperands_AreHandledByTheOperators()
        {
            SurfaceWind? none = null;
            var wind = new SurfaceWind(true, null, null);

            Assert.True(none == null);
            Assert.False(wind == null);
            Assert.True(wind != null);
            Assert.False(null == wind);
        }

        private static object[] Case<T>(string name, Func<Random, T> generate)
            where T : class => new object[] { name, (Func<Random, object>)(r => generate(r)) };

        private static bool InvokeEqualityOperator(object a, object b)
        {
            var op = a.GetType().GetMethod("op_Equality", new[] { a.GetType(), a.GetType() });
            Assert.NotNull(op);
            return (bool)op!.Invoke(null, new[] { a, b })!;
        }

        private static CodeListValue<AerodromeCloudAmount> Amount(AerodromeCloudAmount code, string notation) =>
            new CodeListValue<AerodromeCloudAmount>(code, notation);

        private static double? MaybeDouble(Random r, double scale) =>
            r.Next(4) == 0 ? (double?)null : Math.Round(r.NextDouble() * scale, r.Next(0, 4));

        private static string? MaybeText(Random r, params string[] options) =>
            r.Next(4) == 0 ? null : options[r.Next(options.Length)];

        private static Speed? MaybeSpeed(Random r) =>
            r.Next(3) == 0 ? (Speed?)null : new Speed(r.Next(0, 150), r.Next(2) == 0 ? SpeedUnit.Knots : SpeedUnit.MetersPerSecond);

        private static GeoCoordinate Coordinate(Random r) =>
            new GeoCoordinate(Math.Round((r.NextDouble() * 160) - 80, 4), Math.Round((r.NextDouble() * 340) - 170, 4));

        private static VerticalLimit Limit(Random r) =>
            VerticalLimit.Create(r.Next(0, 450), VerticalUnit.FlightLevel, VerticalReference.Std).Value;

        private static GeoPolygon Polygon(Random r)
        {
            GeoCoordinate start = Coordinate(r);
            var ring = new List<GeoCoordinate>
            {
                start,
                new GeoCoordinate(start.Lat, start.Lon + 1),
                new GeoCoordinate(start.Lat + 1, start.Lon + 1),
                start
            };
            return GeoPolygon.Create(ring).Value;
        }

        private static SurfaceWind Wind(Random r) =>
            new SurfaceWind(
                r.Next(2) == 0,
                MaybeDouble(r, 360),
                MaybeSpeed(r),
                MaybeSpeed(r),
                MaybeOperator(r),
                MaybeOperator(r),
                MaybeDouble(r, 360),
                MaybeDouble(r, 360));

        private static RelationalOperator? MaybeOperator(Random r) =>
            r.Next(3) == 0 ? (RelationalOperator?)null : (r.Next(2) == 0 ? RelationalOperator.Above : RelationalOperator.Below);

        private static CodeListValue<NilReason>? MaybeNilReason(Random r) =>
            r.Next(3) == 0
                ? null
                : (r.Next(2) == 0
                    ? new CodeListValue<NilReason>(NilReason.NothingOfOperationalSignificance, "nothingOfOperationalSignificance")
                    : new CodeListValue<NilReason>(NilReason.NotObservable, "notObservable"));

        private static RunwayVisualRange Rvr(Random r) =>
            new RunwayVisualRange(
                MaybeText(r, "09", "27", "12L"),
                MaybeDouble(r, 2000),
                r.Next(4) == 0 ? (RunwayVisualRangeTendency?)null : (RunwayVisualRangeTendency)r.Next(3),
                MaybeOperator(r));

        private static AerodromeCloudLayer CloudLayer(Random r)
        {
            var amounts = new[] { (AerodromeCloudAmount.Few, "FEW"), (AerodromeCloudAmount.Broken, "BKN"), (AerodromeCloudAmount.Unknown, "XXX") };
            var (code, notation) = amounts[r.Next(amounts.Length)];
            return new AerodromeCloudLayer(
                r.Next(4) == 0 ? null : Amount(code, notation),
                r.Next(4) == 0 ? (Length?)null : new Length(r.Next(1, 100) * 100, LengthUnit.Feet),
                r.Next(2) == 0 ? null : new CodeListValue<ConvectiveCloudType>(ConvectiveCloudType.Cumulonimbus, "CB"),
                MaybeNilReason(r),
                MaybeNilReason(r),
                MaybeNilReason(r));
        }

        private static AerodromeWeather Weather(Random r)
        {
            string[] codes = { "FG", "-DZ", "+TSRA", "http://codes.wmo.int/49-2/AerodromePresentOrForecastWeather/DZ" };
            int index = r.Next(codes.Length);
            return new AerodromeWeather(codes[index], index < 3);
        }

        private static AerodromeForecastGroup ForecastGroup(Random r) =>
            new AerodromeForecastGroup(
                "fg-" + r.Next(5),
                MaybeText(r, "BECOMING", "TEMPORARY_FLUCTUATIONS", "FROM"),
                r.Next(2) == 0,
                r.Next(3) == 0 ? (DateTimeOffset?)null : Epoch.AddHours(r.Next(24)),
                r.Next(3) == 0 ? (DateTimeOffset?)null : Epoch.AddHours(24 + r.Next(24)),
                MaybeDouble(r, 9999),
                MaybeOperator(r),
                r.Next(2) == 0 ? null : Wind(r),
                Enumerable.Range(0, r.Next(3)).Select(_ => Weather(r)).ToList(),
                r.Next(4) == 0
                    ? null
                    : new AerodromeCloud(
                        Enumerable.Range(0, r.Next(3)).Select(_ => CloudLayer(r)).ToList(),
                        r.Next(3) == 0 ? (Length?)null : new Length(r.Next(1, 10) * 100, LengthUnit.Feet),
                        MaybeNilReason(r),
                        MaybeNilReason(r)),
                null,
                MaybeNilReason(r),
                Enumerable.Range(0, r.Next(2)).Select(_ => new AerodromeTemperatureForecast(
                    MaybeDouble(r, 40), r.Next(2) == 0 ? (DateTimeOffset?)null : Epoch.AddHours(r.Next(24)),
                    MaybeDouble(r, 20), r.Next(2) == 0 ? (DateTimeOffset?)null : Epoch.AddHours(r.Next(24)))).ToList());

        private static EnRoutePhenomenonVolume Volume(Random r) =>
            new EnRoutePhenomenonVolume(
                "vol-" + r.Next(5),
                r.Next(3) == 0 ? null : Polygon(r),
                r.Next(3) == 0 ? (VerticalLimit?)null : Limit(r),
                r.Next(3) == 0 ? (VerticalLimit?)null : Limit(r),
                r.Next(3) == 0 ? (IntensityChange?)null : (r.Next(2) == 0 ? IntensityChange.Intensify : IntensityChange.NoChange),
                MaybeDouble(r, 360),
                MaybeSpeed(r),
                r.Next(3) == 0 ? (GeoCoordinate?)null : Coordinate(r),
                r.Next(3) == 0 ? (TimeIndicator?)null : TimeIndicator.Observation,
                r.Next(3) == 0 ? (DateTimeOffset?)null : Epoch.AddHours(r.Next(6)));

        private static JetStreamCorePoint CorePoint(Random r) =>
            new JetStreamCorePoint(
                Coordinate(r),
                Limit(r),
                new Speed(80 + r.Next(100), SpeedUnit.Knots),
                r.Next(2) == 0 ? (VerticalLimit?)null : Limit(r),
                r.Next(2) == 0 ? (VerticalLimit?)null : Limit(r));

        private static JetStream Jet(Random r) =>
            JetStream.Create(
                "jet-" + r.Next(5),
                new[] { Coordinate(r), Coordinate(r), Coordinate(r) },
                Limit(r),
                new Speed(80 + r.Next(100), SpeedUnit.Knots),
                Enumerable.Range(0, r.Next(3)).Select(_ => CorePoint(r)).ToList()).Value;

        private static VolcanicAshConcentrationCloud AshCloud(Random r) =>
            new VolcanicAshConcentrationCloud(
                "ash-" + r.Next(5),
                Polygon(r),
                r.Next(2) == 0 ? (VerticalLimit?)null : Limit(r),
                r.Next(2) == 0 ? (VerticalLimit?)null : Limit(r),
                Math.Round(r.NextDouble() * 10, 2));

        private static TropicalCycloneTrackPoint TrackPoint(Random r) =>
            new TropicalCycloneTrackPoint(
                r.Next(3) == 0 ? (DateTimeOffset?)null : Epoch.AddHours(r.Next(72)),
                Coordinate(r),
                MaybeSpeed(r),
                MaybeDouble(r, 1010));

        private static SpaceWeatherRegion SpaceWeatherRegionCase(Random r) =>
            new SpaceWeatherRegion(
                "reg-" + r.Next(5),
                Enumerable.Range(0, r.Next(3))
                    .Select(_ => r.Next(2) == 0
                        ? new CodeListValue<SpaceWeatherLocation>(SpaceWeatherLocation.HighLatitudesNorth, "HNH")
                        : new CodeListValue<SpaceWeatherLocation>(SpaceWeatherLocation.Unknown, "DAYLIGHT_SIDE"))
                    .ToList(),
                Enumerable.Range(0, r.Next(3)).Select(_ => Polygon(r)).ToList(),
                r.Next(2) == 0 ? (VerticalLimit?)null : Limit(r),
                r.Next(2) == 0 ? (VerticalLimit?)null : Limit(r));

        private static SpaceWeatherIntensityAndRegion SpaceWeatherIntensityAndRegionCase(Random r) =>
            new SpaceWeatherIntensityAndRegion(
                "swx-" + r.Next(5),
                MaybeText(r, "MOD", "SEV"),
                Enumerable.Range(0, r.Next(3)).Select(_ => SpaceWeatherRegionCase(r)).ToList());

        private static SpaceWeatherAnalysis SpaceWeatherAnalysisCase(Random r) =>
            new SpaceWeatherAnalysis(
                "analysis-" + r.Next(5),
                r.Next(3) == 0 ? (TimeIndicator?)null : (r.Next(2) == 0 ? TimeIndicator.Observation : TimeIndicator.Forecast),
                r.Next(3) == 0 ? (DateTimeOffset?)null : Epoch.AddHours(r.Next(24)),
                Enumerable.Range(0, r.Next(3)).Select(_ => SpaceWeatherIntensityAndRegionCase(r)).ToList(),
                MaybeNilReason(r));

        private static VolcanicAshConditions AshConditions(Random r) =>
            new VolcanicAshConditions(
                "fcst-" + r.Next(4),
                r.Next(3) == 0 ? (DateTimeOffset?)null : Epoch.AddHours(6 * r.Next(4)),
                r.Next(3) == 0 ? (VolcanicAshConditionsStatus?)null : VolcanicAshConditionsStatus.NoVolcanicAshExpected,
                Enumerable.Range(0, r.Next(3)).Select(_ => Volume(r)).ToList());

        private static AirmetEvolvingCondition AirmetCondition(Random r) =>
            new AirmetEvolvingCondition(
                Volume(r),
                r.Next(2) == 0 ? (VerticalLimit?)null : VerticalLimit.Create(r.Next(5) * 500, VerticalUnit.Feet, VerticalReference.Sfc).Value,
                null,
                r.Next(3) == 0 ? (bool?)null : r.Next(2) == 0,
                r.Next(2) == 0 ? (Length?)null : new Length(r.Next(5) * 1000, LengthUnit.Meters),
                Enumerable.Range(0, r.Next(3))
                    .Select(_ => new CodeListValue<VisibilityReductionCause>(VisibilityReductionCause.Fog, "FG"))
                    .ToList(),
                r.Next(2) == 0 ? (double?)null : r.Next(36) * 10,
                MaybeSpeed(r));

        private static CodeListValue<SigmetPhenomenon> PhenomenonCode(Random r) =>
            r.Next(2) == 0
                ? new CodeListValue<SigmetPhenomenon>(SigmetPhenomenon.VolcanicAsh, "VA")
                : new CodeListValue<SigmetPhenomenon>(SigmetPhenomenon.Unknown, "http://example.invalid/" + r.Next(3));
    }
}
