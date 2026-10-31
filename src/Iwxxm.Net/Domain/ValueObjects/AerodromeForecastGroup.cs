using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing a trend or change forecast group (e.g., BECOMING, TEMPORARY_FLUCTUATIONS, FROM).
    /// <see cref="Weather"/> holds the forecast weather codes (WMO code list <c>AerodromePresentOrForecastWeather</c>);
    /// <see cref="WeatherNilReason"/> distinguishes "no significant weather" (TAC <c>NSW</c>,
    /// <see cref="NilReason.NothingOfOperationalSignificance"/>) from weather that is not forecast at all.
    /// <see cref="Cloud"/> is <c>null</c> when no cloud group is reported (see <see cref="AerodromeCloud"/> for <c>NSC</c>
    /// and vertical visibility). <see cref="Temperatures"/> holds the TAF <c>TX/TN</c> pairs (empty for trends).
    /// Equality is structural and exact; collections are compared by content, in order.
    /// </summary>
    public sealed class AerodromeForecastGroup : IEquatable<AerodromeForecastGroup>
    {
        public string Id { get; }
        public string? ChangeIndicator { get; }
        public bool CloudAndVisibilityOk { get; }
        public DateTimeOffset? ValidFromUtc { get; }
        public DateTimeOffset? ValidUntilUtc { get; }
        public double? PrevailingVisibilityMeters { get; }
        public RelationalOperator? PrevailingVisibilityOperator { get; }
        public SurfaceWind? SurfaceWind { get; }
        public IReadOnlyList<AerodromeWeather> Weather { get; }

        /// <summary>Reason why the forecast weather is nil (<c>NSW</c>); <c>null</c> when weather is given or not reported.</summary>
        public CodeListValue<NilReason>? WeatherNilReason { get; }

        /// <summary>Gets a value indicating whether the group forecasts the end of significant weather (TAC <c>NSW</c>).</summary>
        public bool IsNoSignificantWeather => WeatherNilReason?.Code == NilReason.NothingOfOperationalSignificance;

        /// <summary>Cloud group (layers, vertical visibility, <c>NSC</c>); <c>null</c> when not reported.</summary>
        public AerodromeCloud? Cloud { get; }

        /// <summary>Cloud layers of <see cref="Cloud"/> (empty when no cloud group is reported).</summary>
        public IReadOnlyList<AerodromeCloudLayer> CloudLayers => Cloud?.Layers ?? Array.Empty<AerodromeCloudLayer>();

        /// <summary>TAF maximum/minimum temperature forecasts (<c>TX</c>/<c>TN</c>), in document order.</summary>
        public IReadOnlyList<AerodromeTemperatureForecast> Temperatures { get; }

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
            RelationalOperator? prevailingVisibilityOperator,
            SurfaceWind? surfaceWind,
            IEnumerable<AerodromeWeather>? weather = null,
            AerodromeCloud? cloud = null,
            TrendTimeIndicator? timeIndicator = null,
            CodeListValue<NilReason>? weatherNilReason = null,
            IEnumerable<AerodromeTemperatureForecast>? temperatures = null)
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
            Cloud = cloud;
            TimeIndicator = timeIndicator;
            WeatherNilReason = weatherNilReason;
            Temperatures = new ReadOnlyCollection<AerodromeTemperatureForecast>(temperatures?.ToList() ?? new List<AerodromeTemperatureForecast>(0));
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
                   PrevailingVisibilityOperator == other.PrevailingVisibilityOperator &&
                   Equals(SurfaceWind, other.SurfaceWind) &&
                   ValueEquality.SequenceEqual(Weather, other.Weather) &&
                   Equals(Cloud, other.Cloud) &&
                   TimeIndicator == other.TimeIndicator &&
                   Equals(WeatherNilReason, other.WeatherNilReason) &&
                   ValueEquality.SequenceEqual(Temperatures, other.Temperatures);
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
            hash = ValueEquality.Add(hash, Cloud);
            hash = ValueEquality.Add(hash, TimeIndicator);
            hash = ValueEquality.Add(hash, WeatherNilReason);
            return ValueEquality.AddSequence(hash, Temperatures);
        }

        public static bool operator ==(AerodromeForecastGroup? left, AerodromeForecastGroup? right) => Equals(left, right);

        public static bool operator !=(AerodromeForecastGroup? left, AerodromeForecastGroup? right) => !Equals(left, right);
    }
}
