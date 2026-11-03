using System;
using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Verifies the homogeneous physical / structural invariants enforced by every aggregate <c>Create</c> factory
    /// (DDD-05, AUD-L10 invariants part). Business rules of the official Schematron are intentionally not duplicated.
    /// </summary>
    public sealed class AggregateInvariantTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset T1 = T0.AddHours(4);

        // ---------------------------------------------------------------------
        // Validity periods: ValidUntil > ValidFrom when both are present (a forecast group and a SIGWX chart may be an instant)
        // ---------------------------------------------------------------------

        [Fact]
        public void Sigmet_InvertedOrEmptyValidPeriod_IsRejected()
        {
            AssertError(CreateSigmet(validFrom: T1, validUntil: T0), "DOMAIN-SIGMET-VALID-PERIOD");
            AssertError(CreateSigmet(validFrom: T0, validUntil: T0), "DOMAIN-SIGMET-VALID-PERIOD");
            Assert.True(CreateSigmet(validFrom: T0, validUntil: T1).IsSuccess);
            Assert.True(CreateSigmet(validFrom: null, validUntil: null).IsSuccess); // period given by unresolved reference
        }

        [Fact]
        public void Airmet_InvertedValidPeriod_IsRejected()
        {
            AssertError(CreateAirmet(atsUnit: "YUDD", validFrom: T1, validUntil: T0), "DOMAIN-AIRMET-VALID-PERIOD");
            Assert.True(CreateAirmet(atsUnit: "YUDD", validFrom: T0, validUntil: T1).IsSuccess);
        }

        [Fact]
        public void Airmet_EmptyAtsUnit_IsRejectedLikeSigmet()
        {
            AssertError(CreateAirmet(atsUnit: " ", validFrom: T0, validUntil: T1), "DOMAIN-AIRMET-ATS-UNIT");
            AssertError(CreateSigmet(validFrom: T0, validUntil: T1, atsUnit: ""), "DOMAIN-SIGMET-ATS-UNIT");
        }

        [Fact]
        public void Taf_InvertedValidAndCancelledPeriods_AreRejected()
        {
            AssertError(Taf.Create("t1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "YUDO", null, null, validFromUtc: T1, validUntilUtc: T0), "DOMAIN-TAF-VALID-PERIOD");
            AssertError(Taf.Create("t1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "YUDO", null, null, isCancelReport: true, cancelledReportValidFromUtc: T1, cancelledReportValidUntilUtc: T0), "DOMAIN-TAF-CANCELLED-VALID-PERIOD");
        }

        [Fact]
        public void ForecastGroup_InstantPeriod_IsAccepted_AudR2()
        {
            // A trend or change group valid at a single instant (gml:TimePeriod with begin == end) is allowed by the schema.
            var instant = new AerodromeForecastGroup("g1", "BECOMING", false, T0, T0, null, null, null);

            Assert.True(Taf.Create("t1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "YUDO", null, null, baseForecast: instant).IsSuccess);
            Assert.True(CreateMetar(trend: instant).IsSuccess);
        }

        [Fact]
        public void Sigwx_InstantValidity_IsAccepted_ButAnInvertedOneIsRejected_AudR2()
        {
            Assert.True(CreateSigwx(T0, T0).IsSuccess);
            Assert.True(CreateSigwx(T0, T1).IsSuccess);

            Result<Sigwx> inverted = CreateSigwx(T1, T0);
            AssertError(inverted, "DOMAIN-SIGWX-VALID-PERIOD");
            Assert.Contains("must not be before", inverted.Errors.First(e => e.Code == "DOMAIN-SIGWX-VALID-PERIOD").Message);
        }

        [Fact]
        public void ForecastGroup_InvertedPeriod_IsRejected()
        {
            var group = new AerodromeForecastGroup("g1", "BECOMING", false, T1, T0, null, null, null);

            AssertError(Taf.Create("t1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "YUDO", null, null, baseForecast: group), "DOMAIN-FORECAST-GROUP-PERIOD");
            AssertError(CreateMetar(trend: group), "DOMAIN-FORECAST-GROUP-PERIOD");
        }

        // ---------------------------------------------------------------------
        // *Utc properties must carry a zero UTC offset; issue time is mandatory
        // ---------------------------------------------------------------------

        [Fact]
        public void NonUtcTimestamps_AreRejected()
        {
            DateTimeOffset local = new DateTimeOffset(2026, 3, 1, 14, 0, 0, TimeSpan.FromHours(2));

            AssertError(CreateMetar(issueTime: local), "DOMAIN-TIME-NOT-UTC");
            AssertError(CreateMetar(observationTime: local), "DOMAIN-TIME-NOT-UTC");
            AssertError(CreateSigmet(validFrom: local, validUntil: T1), "DOMAIN-TIME-NOT-UTC");
            AssertError(Taf.Create("t1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "YUDO", null, null, validFromUtc: T0, validUntilUtc: local.AddDays(1)), "DOMAIN-TIME-NOT-UTC");
            AssertError(Vona.Create("v1", local, ReportStatus.Normal, PermissibleUsage.Operational, "KVERT"), "DOMAIN-TIME-NOT-UTC");
            AssertError(Qvaci.Create("q1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "Darwin", dispersionModelInitialisationTimeUtc: local), "DOMAIN-TIME-NOT-UTC");
            AssertError(Vaa.Create("a1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "TOKYO", nextAdvisoryTimeUtc: local), "DOMAIN-TIME-NOT-UTC");
            AssertError(Tca.Create("c1", local, ReportStatus.Normal, PermissibleUsage.Operational, "YUFO"), "DOMAIN-TIME-NOT-UTC");
            AssertError(SpaceWx.Create("s1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "DONLON", nextAdvisoryTimeUtc: local), "DOMAIN-TIME-NOT-UTC");
        }

        [Fact]
        public void DefaultIssueTime_IsRejectedAsMissing()
        {
            AssertError(CreateMetar(issueTime: default(DateTimeOffset)), "DOMAIN-ISSUE-TIME-MISSING");
            AssertError(Vona.Create("v1", DateTimeOffset.MinValue, ReportStatus.Normal, PermissibleUsage.Operational, "KVERT"), "DOMAIN-ISSUE-TIME-MISSING");
        }

        // ---------------------------------------------------------------------
        // Physical ranges: wind direction 0-360, non-negative speed / visibility / RVR
        // ---------------------------------------------------------------------

        [Theory]
        [InlineData(-1.0)]
        [InlineData(360.5)]
        public void WindDirectionOutside0To360_IsRejected(double direction)
        {
            var wind = new SurfaceWind(false, direction, new Speed(5, SpeedUnit.Knots));

            AssertError(CreateMetar(wind: wind), "DOMAIN-DIRECTION-OUT-OF-RANGE");
        }

        [Fact]
        public void WindDirectionAtBounds_IsAccepted()
        {
            Assert.True(CreateMetar(wind: new SurfaceWind(false, 0, new Speed(5, SpeedUnit.Knots))).IsSuccess);
            Assert.True(CreateMetar(wind: new SurfaceWind(false, 360, new Speed(5, SpeedUnit.Knots))).IsSuccess);
        }

        [Fact]
        public void NegativeSpeedsVisibilityAndRvr_AreRejected()
        {
            AssertError(CreateMetar(wind: new SurfaceWind(false, 90, new Speed(-1, SpeedUnit.MetersPerSecond))), "DOMAIN-NEGATIVE-SPEED");
            AssertError(CreateMetar(wind: new SurfaceWind(false, 90, new Speed(5, SpeedUnit.Knots), new Speed(-3, SpeedUnit.Knots))), "DOMAIN-NEGATIVE-SPEED");
            AssertError(CreateMetar(visibility: -50), "DOMAIN-NEGATIVE-VISIBILITY");
            AssertError(CreateMetar(rvr: new RunwayVisualRange("27", -10)), "DOMAIN-NEGATIVE-RVR");
        }

        [Fact]
        public void EnRouteVolume_MotionOutOfRange_IsRejected()
        {
            var badDirection = new EnRoutePhenomenonVolume("c1", null, null, null, null, 400, null);
            var badSpeed = new EnRoutePhenomenonVolume("c2", null, null, null, null, 90, new Speed(-5, SpeedUnit.Knots));

            AssertError(CreateSigmet(validFrom: T0, validUntil: T1, volume: badDirection), "DOMAIN-DIRECTION-OUT-OF-RANGE");
            AssertError(CreateSigmet(validFrom: T0, validUntil: T1, volume: badSpeed), "DOMAIN-NEGATIVE-SPEED");
        }

        [Fact]
        public void TropicalCycloneAdvisory_NegativeMaximumWind_IsRejected()
        {
            var point = new TropicalCycloneTrackPoint(T0, new GeoCoordinate(10, 10), new Speed(-1, SpeedUnit.Knots));

            AssertError(Tca.Create("c1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "YUFO", observation: point), "DOMAIN-NEGATIVE-SPEED");
        }

        [Fact]
        public void Factories_ReportEveryViolation_NotOnlyTheFirst()
        {
            var result = Sigmet.Create(
                "", IwxxmReportType.Sigmet, T0, ReportStatus.Normal, PermissibleUsage.Operational,
                "", null, null, null, null, null, T1, T0);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "DOMAIN-SIGMET-ID");
            Assert.Contains(result.Errors, e => e.Code == "DOMAIN-SIGMET-ATS-UNIT");
            Assert.Contains(result.Errors, e => e.Code == "DOMAIN-SIGMET-VALID-PERIOD");
            Assert.All(result.Errors, e => Assert.Equal(ErrorCategory.DomainMapping, e.Category));
        }

        private static void AssertError<T>(Result<T> result, string code)
        {
            Assert.True(result.IsFailure, $"Expected '{code}' but the factory succeeded.");
            Assert.Contains(result.Errors, e => e.Code == code);
        }

        private static Result<Metar> CreateMetar(
            DateTimeOffset? issueTime = null,
            DateTimeOffset? observationTime = null,
            SurfaceWind? wind = null,
            double? visibility = null,
            RunwayVisualRange? rvr = null,
            AerodromeForecastGroup? trend = null)
        {
            return Metar.Create(
                "m1",
                issueTime ?? T0,
                observationTime ?? T0,
                ReportStatus.Normal,
                PermissibleUsage.Operational,
                false,
                "YUDO",
                null,
                null,
                surfaceWind: wind,
                visibility: visibility.HasValue ? new AerodromeHorizontalVisibility(visibility) : null,
                runwayVisualRanges: rvr == null ? null : new[] { rvr },
                trendForecasts: trend == null ? null : new[] { trend });
        }

        private static Result<Sigwx> CreateSigwx(DateTimeOffset validFrom, DateTimeOffset validUntil)
        {
            var spot = new TropopauseSpotHeight(new GeoCoordinate(10, 20), VerticalLimit.Create(350, VerticalUnit.FlightLevel, VerticalReference.Std).Value);
            return Sigwx.Create(
                "w1", "London", ReportStatus.Normal, PermissibleUsage.Operational, T0, validFrom, validUntil,
                VerticalLimit.Create(100, VerticalUnit.FlightLevel, VerticalReference.Std).Value,
                VerticalLimit.Create(600, VerticalUnit.FlightLevel, VerticalReference.Std).Value,
                tropopauses: new[] { Tropopause.Create("t1", null, new[] { spot }).Value });
        }

        private static Result<Sigmet> CreateSigmet(DateTimeOffset? validFrom, DateTimeOffset? validUntil, string atsUnit = "YUDD", EnRoutePhenomenonVolume? volume = null)
        {
            return Sigmet.Create(
                "s1", IwxxmReportType.Sigmet, T0, ReportStatus.Normal, PermissibleUsage.Operational,
                atsUnit, null, null, null, null, "1", validFrom, validUntil,
                evolvingConditions: volume == null ? null : new List<EnRoutePhenomenonVolume> { volume });
        }

        private static Result<Airmet> CreateAirmet(string atsUnit, DateTimeOffset? validFrom, DateTimeOffset? validUntil)
        {
            return Airmet.Create(
                "a1", T0, ReportStatus.Normal, PermissibleUsage.Operational,
                atsUnit, null, null, null, null, "1", validFrom, validUntil);
        }
    }
}
