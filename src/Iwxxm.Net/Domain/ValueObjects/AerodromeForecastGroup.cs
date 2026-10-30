using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing a trend or change forecast group (e.g., BECOMING, TEMPORARY_FLUCTUATIONS, FROM).
    /// <see cref="Weather"/> holds the forecast weather codes (WMO code list <c>AerodromePresentOrForecastWeather</c>).
    /// Equality is structural and exact; <see cref="Weather"/> and <see cref="CloudLayers"/> are compared by content, in order.
    /// </summary>
    public sealed class AerodromeForecastGroup : IEquatable<AerodromeForecastGroup>
    {
        public string Id { get; }
        public string? ChangeIndicator { get; }
        public bool CloudAndVisibilityOk { get; }
        public DateTimeOffset? ValidFromUtc { get; }
        public DateTimeOffset? ValidUntilUtc { get; }
        public double? PrevailingVisibilityMeters { get; }
        public string? PrevailingVisibilityOperator { get; }
        public SurfaceWind? SurfaceWind { get; }
        public IReadOnlyList<AerodromeWeather> Weather { get; }
        public IReadOnlyList<AerodromeCloudLayer> CloudLayers { get; }

        /// <summary>
        /// METAR/SPECI trend <c>timeIndicator</c> (<c>AT</c>, <c>UNTIL</c>, <c>FROM</c>); <c>null</c> when absent (always for TAF
        /// groups). The trend time is <see cref="ValidFromUtc"/> (instant or period start) and <see cref="ValidUntilUtc"/>.
        /// </summary>
        public TrendTimeIndicator? TimeIndicator { get; }

        public AerodromeForecastGroup(
            string id,
            string? changeIndicator,
            bool cloudAndVisibilityOk,
            DateTimeOffset? validFromUtc,
            DateTimeOffset? validUntilUtc,
            double? prevailingVisibilityMeters,
            string? prevailingVisibilityOperator,
            SurfaceWind? surfaceWind,
            IEnumerable<AerodromeWeather>? weather = null,
            IEnumerable<AerodromeCloudLayer>? cloudLayers = null,
            TrendTimeIndicator? timeIndicator = null)
        {
            Id = id ?? string.Empty;
            ChangeIndicator = changeIndicator;
            CloudAndVisibilityOk = cloudAndVisibilityOk;
            ValidFromUtc = validFromUtc;
            ValidUntilUtc = validUntilUtc;
            PrevailingVisibilityMeters = prevailingVisibilityMeters;
            PrevailingVisibilityOperator = prevailingVisibilityOperator;
            SurfaceWind = surfaceWind;
            Weather = new ReadOnlyCollection<AerodromeWeather>(weather?.ToList() ?? new List<AerodromeWeather>(0));
            CloudLayers = new ReadOnlyCollection<AerodromeCloudLayer>(cloudLayers?.ToList() ?? new List<AerodromeCloudLayer>(0));
            TimeIndicator = timeIndicator;
        }

        public bool Equals(AerodromeForecastGroup? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                   string.Equals(ChangeIndicator, other.ChangeIndicator, StringComparison.Ordinal) &&
                   CloudAndVisibilityOk == other.CloudAndVisibilityOk &&
                   Nullable.Equals(ValidFromUtc, other.ValidFromUtc) &&
                   Nullable.Equals(ValidUntilUtc, other.ValidUntilUtc) &&
                   Nullable.Equals(PrevailingVisibilityMeters, other.PrevailingVisibilityMeters) &&
                   string.Equals(PrevailingVisibilityOperator, other.PrevailingVisibilityOperator, StringComparison.Ordinal) &&
                   Equals(SurfaceWind, other.SurfaceWind) &&
                   ValueEquality.SequenceEqual(Weather, other.Weather) &&
                   ValueEquality.SequenceEqual(CloudLayers, other.CloudLayers) &&
                   TimeIndicator == other.TimeIndicator;
        }

        public override bool Equals(object? obj) => obj is AerodromeForecastGroup other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Id);
            hash = ValueEquality.Add(hash, ChangeIndicator);
            hash = ValueEquality.Add(hash, CloudAndVisibilityOk);
            hash = ValueEquality.Add(hash, ValidFromUtc);
            hash = ValueEquality.Add(hash, ValidUntilUtc);
            hash = ValueEquality.AddDouble(hash, PrevailingVisibilityMeters);
            hash = ValueEquality.Add(hash, PrevailingVisibilityOperator);
            hash = ValueEquality.Add(hash, SurfaceWind);
            hash = ValueEquality.AddSequence(hash, Weather);
            hash = ValueEquality.AddSequence(hash, CloudLayers);
            return ValueEquality.Add(hash, TimeIndicator);
        }

        public static bool operator ==(AerodromeForecastGroup? left, AerodromeForecastGroup? right) => Equals(left, right);

        public static bool operator !=(AerodromeForecastGroup? left, AerodromeForecastGroup? right) => !Equals(left, right);
    }
}
