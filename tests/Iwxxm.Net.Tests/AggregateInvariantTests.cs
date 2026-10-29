using System;
using System.Collections.Generic;
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
        // Validity periods: ValidUntil > ValidFrom when both are present
        // ---------------------------------------------------------------------

        [Fact]
        public void Sigmet_InvertedOrEmptyValidPeriod_IsRejected()
        {
            AssertError(Sigmet(validFrom: T1, validUntil: T0), "DOMAIN-SIGMET-VALID-PERIOD");
            AssertError(Sigmet(validFrom: T0, validUntil: T0), "DOMAIN-SIGMET-VALID-PERIOD");
            Assert.True(Sigmet(validFrom: T0, validUntil: T1).IsSuccess);
            Assert.True(Sigmet(validFrom: null, validUntil: null).IsSuccess); // period given by unresolved reference
        }

        [Fact]
        public void Airmet_InvertedValidPeriod_IsRejected()
        {
            AssertError(Airmet(atsUnit: "YUDD", validFrom: T1, validUntil: T0), "DOMAIN-AIRMET-VALID-PERIOD");
            Assert.True(Airmet(atsUnit: "YUDD", validFrom: T0, validUntil: T1).IsSuccess);
        }

        [Fact]
        public void Airmet_EmptyAtsUnit_IsRejectedLikeSigmet()
        {
            AssertError(Airmet(atsUnit: " ", validFrom: T0, validUntil: T1), "DOMAIN-AIRMET-ATS-UNIT");
            AssertError(Sigmet(validFrom: T0, validUntil: T1, atsUnit: ""), "DOMAIN-SIGMET-ATS-UNIT");
        }

        [Fact]
        public void Taf_InvertedValidAndCancelledPeriods_AreRejected()
        {
            AssertError(TafReport.Create("t1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "YUDO", null, null, validFromUtc: T1, validUntilUtc: T0), "DOMAIN-TAF-VALID-PERIOD");
            AssertError(TafReport.Create("t1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "YUDO", null, null, isCancelReport: true, cancelledReportValidFromUtc: T1, cancelledReportValidUntilUtc: T0), "DOMAIN-TAF-CANCELLED-VALID-PERIOD");
        }

        [Fact]
        public void ForecastGroup_InvertedPeriod_IsRejected()
        {
            var group = new AerodromeForecastGroupInfo("g1", "BECOMING", false, T1, T0, null, null, null);

            AssertError(TafReport.Create("t1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "YUDO", null, null, baseForecast: group), "DOMAIN-FORECAST-GROUP-PERIOD");
            AssertError(Metar(trend: group), "DOMAIN-FORECAST-GROUP-PERIOD");
        }

        // ---------------------------------------------------------------------
        // *Utc properties must carry a zero UTC offset; issue time is mandatory
        // ---------------------------------------------------------------------

        [Fact]
        public void NonUtcTimestamps_AreRejected()
        {
            DateTimeOffset local = new DateTimeOffset(2026, 3, 1, 14, 0, 0, TimeSpan.FromHours(2));

            AssertError(Metar(issueTime: local), "DOMAIN-TIME-NOT-UTC");
            AssertError(Metar(observationTime: local), "DOMAIN-TIME-NOT-UTC");
            AssertError(Sigmet(validFrom: local, validUntil: T1), "DOMAIN-TIME-NOT-UTC");
            AssertError(TafReport.Create("t1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "YUDO", null, null, validFromUtc: T0, validUntilUtc: local.AddDays(1)), "DOMAIN-TIME-NOT-UTC");
            AssertError(VonaReport.Create("v1", local, ReportStatus.Normal, PermissibleUsage.Operational, "KVERT"), "DOMAIN-TIME-NOT-UTC");
            AssertError(QvaciReport.Create("q1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "Darwin", dispersionModelInitialisationTimeUtc: local), "DOMAIN-TIME-NOT-UTC");
            AssertError(VolcanicAshAdvisoryReport.Create("a1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "TOKYO", nextAdvisoryTimeUtc: local), "DOMAIN-TIME-NOT-UTC");
            AssertError(TropicalCycloneAdvisoryReport.Create("c1", local, ReportStatus.Normal, PermissibleUsage.Operational, "YUFO"), "DOMAIN-TIME-NOT-UTC");
            AssertError(SpaceWeatherAdvisoryReport.Create("s1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "DONLON", nextAdvisoryTimeUtc: local), "DOMAIN-TIME-NOT-UTC");
        }

        [Fact]
        public void DefaultIssueTime_IsRejectedAsMissing()
        {
            AssertError(Metar(issueTime: default(DateTimeOffset)), "DOMAIN-ISSUE-TIME-MISSING");
            AssertError(VonaReport.Create("v1", DateTimeOffset.MinValue, ReportStatus.Normal, PermissibleUsage.Operational, "KVERT"), "DOMAIN-ISSUE-TIME-MISSING");
        }

        // ---------------------------------------------------------------------
        // Physical ranges: wind direction 0-360, non-negative speed / visibility / RVR
        // ---------------------------------------------------------------------

        [Theory]
        [InlineData(-1.0)]
        [InlineData(360.5)]
        public void WindDirectionOutside0To360_IsRejected(double direction)
        {
            var wind = new SurfaceWindInfo(false, direction, new Speed(5, SpeedUnit.Knots));

            AssertError(Metar(wind: wind), "DOMAIN-DIRECTION-OUT-OF-RANGE");
        }

        [Fact]
        public void WindDirectionAtBounds_IsAccepted()
        {
            Assert.True(Metar(wind: new SurfaceWindInfo(false, 0, new Speed(5, SpeedUnit.Knots))).IsSuccess);
            Assert.True(Metar(wind: new SurfaceWindInfo(false, 360, new Speed(5, SpeedUnit.Knots))).IsSuccess);
        }

        [Fact]
        public void NegativeSpeedsVisibilityAndRvr_AreRejected()
        {
            AssertError(Metar(wind: new SurfaceWindInfo(false, 90, new Speed(-1, SpeedUnit.MetersPerSecond))), "DOMAIN-NEGATIVE-SPEED");
            AssertError(Metar(wind: new SurfaceWindInfo(false, 90, new Speed(5, SpeedUnit.Knots), new Speed(-3, SpeedUnit.Knots))), "DOMAIN-NEGATIVE-SPEED");
            AssertError(Metar(visibility: -50), "DOMAIN-NEGATIVE-VISIBILITY");
            AssertError(Metar(rvr: new RunwayVisualRangeInfo("27", -10)), "DOMAIN-NEGATIVE-RVR");
        }

        [Fact]
        public void EnRouteVolume_MotionOutOfRange_IsRejected()
        {
            var badDirection = new EnRoutePhenomenonVolume("c1", null, null, null, null, 400, null);
            var badSpeed = new EnRoutePhenomenonVolume("c2", null, null, null, null, 90, new Speed(-5, SpeedUnit.Knots));

            AssertError(Sigmet(validFrom: T0, validUntil: T1, volume: badDirection), "DOMAIN-DIRECTION-OUT-OF-RANGE");
            AssertError(Sigmet(validFrom: T0, validUntil: T1, volume: badSpeed), "DOMAIN-NEGATIVE-SPEED");
        }

        [Fact]
        public void TropicalCycloneAdvisory_NegativeMaximumWind_IsRejected()
        {
            var point = new TropicalCycloneTrackPoint(T0, new GeoCoordinate(10, 10), new Speed(-1, SpeedUnit.Knots));

            AssertError(TropicalCycloneAdvisoryReport.Create("c1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "YUFO", observation: point), "DOMAIN-NEGATIVE-SPEED");
        }

        [Fact]
        public void Factories_ReportEveryViolation_NotOnlyTheFirst()
        {
            var result = SigmetReport.Create(
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

        private static Result<AerodromeWeatherReport> Metar(
            DateTimeOffset? issueTime = null,
            DateTimeOffset? observationTime = null,
            SurfaceWindInfo? wind = null,
            double? visibility = null,
            RunwayVisualRangeInfo? rvr = null,
            AerodromeForecastGroupInfo? trend = null)
        {
            return AerodromeWeatherReport.Create(
                "m1",
                IwxxmReportType.Metar,
                issueTime ?? T0,
                observationTime ?? T0,
                ReportStatus.Normal,
                PermissibleUsage.Operational,
                false,
                "YUDO",
                null,
                null,
                surfaceWind: wind,
                prevailingVisibilityMeters: visibility,
                runwayVisualRanges: rvr == null ? null : new[] { rvr },
                trendForecasts: trend == null ? null : new[] { trend });
        }

        private static Result<SigmetReport> Sigmet(DateTimeOffset? validFrom, DateTimeOffset? validUntil, string atsUnit = "YUDD", EnRoutePhenomenonVolume? volume = null)
        {
            return SigmetReport.Create(
                "s1", IwxxmReportType.Sigmet, T0, ReportStatus.Normal, PermissibleUsage.Operational,
                atsUnit, null, null, null, null, "1", validFrom, validUntil,
                evolvingConditions: volume == null ? null : new List<EnRoutePhenomenonVolume> { volume });
        }

        private static Result<AirmetReport> Airmet(string atsUnit, DateTimeOffset? validFrom, DateTimeOffset? validUntil)
        {
            return AirmetReport.Create(
                "a1", T0, ReportStatus.Normal, PermissibleUsage.Operational,
                atsUnit, null, null, null, null, "1", validFrom, validUntil);
        }
    }
}
