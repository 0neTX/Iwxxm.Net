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
        /// <summary>Gets the <c>gml:id</c> of the forecast record (<c>MeteorologicalAerodromeForecast</c> of a TAF or <c>MeteorologicalAerodromeTrendForecast</c> of a METAR/SPECI); an empty string when the source has none.</summary>
        public string Id { get; }
        /// <summary>
        /// Gets the change indicator exactly as written in the document, or <c>null</c> for the base conditions of a TAF, which carry none.
        /// A TAF uses <c>BECOMING</c> (BECMG), <c>TEMPORARY_FLUCTUATIONS</c> (TEMPO), <c>FROM</c> (FM), <c>PROBABILITY_30</c>, <c>PROBABILITY_40</c>,
        /// <c>PROBABILITY_30_TEMPORARY_FLUCTUATIONS</c> and <c>PROBABILITY_40_TEMPORARY_FLUCTUATIONS</c>; a METAR/SPECI trend uses <c>BECOMING</c> or <c>TEMPORARY_FLUCTUATIONS</c> (a nil trend is <c>NOSIG</c>).
        /// </summary>
        public string? ChangeIndicator { get; }
        /// <summary>Gets a value indicating whether the group forecasts <c>CAVOK</c> (<c>cloudAndVisibilityOK="true"</c>): visibility 10 km or more, no cloud of operational significance and no weather of significance to aviation, so visibility, weather and cloud are not reported.</summary>
        public bool CloudAndVisibilityOk { get; }
        /// <summary>Gets the start of the period the group applies to (<c>phenomenonTime</c> of the forecast record: the FM time, the start of a BECMG or TEMPO period, or the instant of an <c>AT</c> trend), or <c>null</c> when not given.</summary>
        public DateTimeOffset? ValidFromUtc { get; }
        /// <summary>Gets the end of the period the group applies to, or <c>null</c> when the time is an instant or is not given. The group factories reject an end before the start; an equal end is accepted.</summary>
        public DateTimeOffset? ValidUntilUtc { get; }
        /// <summary>Gets the forecast prevailing horizontal visibility in metres (<c>iwxxm:prevailingVisibility</c>), or <c>null</c> when not reported (mandatory in a TAF except with CAVOK).</summary>
        public double? PrevailingVisibilityMeters { get; }
        /// <summary>Gets the qualifier of <see cref="PrevailingVisibilityMeters"/> (<see cref="RelationalOperator.Above"/> for 10000 m or more), or <c>null</c> when it is an exact value.</summary>
        public RelationalOperator? PrevailingVisibilityOperator { get; }
        /// <summary>Gets the forecast surface wind (<c>iwxxm:surfaceWind</c>), or <c>null</c> when not reported.</summary>
        public SurfaceWind? SurfaceWind { get; }
        /// <summary>Gets the forecast weather codes (WMO code list <c>AerodromePresentOrForecastWeather</c>) in document order; empty when none is forecast, see <see cref="WeatherNilReason"/> for <c>NSW</c>.</summary>
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

        /// <summary>Creates a forecast group. The report factories check the period order, non-negative visibility and the wind and cloud ranges of every group.</summary>
        /// <param name="id">The <c>gml:id</c> of the forecast record; <c>null</c> is stored as an empty string.</param>
        /// <param name="changeIndicator">The change indicator as written in the document; <c>null</c> for the base conditions of a TAF.</param>
        /// <param name="cloudAndVisibilityOk">Whether <c>CAVOK</c> is forecast.</param>
        /// <param name="validFromUtc">The start of the period in UTC; <c>null</c> when not given.</param>
        /// <param name="validUntilUtc">The end of the period in UTC; <c>null</c> for an instant or when not given.</param>
        /// <param name="prevailingVisibilityMeters">The prevailing visibility in metres; <c>null</c> when not reported.</param>
        /// <param name="prevailingVisibilityOperator">The qualifier of the prevailing visibility; <c>null</c> for an exact value.</param>
        /// <param name="surfaceWind">The surface wind; <c>null</c> when not reported.</param>
        /// <param name="weather">The forecast weather codes; <c>null</c> is treated as empty.</param>
        /// <param name="cloud">The cloud group; <c>null</c> when not reported.</param>
        /// <param name="timeIndicator">The trend time indicator (<c>AT</c>, <c>UNTIL</c>, <c>FROM</c>) of a METAR/SPECI trend; <c>null</c> otherwise.</param>
        /// <param name="weatherNilReason">Why the forecast weather is nil (<c>nothingOfOperationalSignificance</c> for <c>NSW</c>); <c>null</c> otherwise.</param>
        /// <param name="temperatures">The TAF maximum and minimum temperature forecasts; <c>null</c> is treated as empty.</param>
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is AerodromeForecastGroup other && Equals(other);

        /// <inheritdoc />
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

        /// <summary>Returns <c>true</c> when the two <see cref="AerodromeForecastGroup"/> operands are equal by value.</summary>
        public static bool operator ==(AerodromeForecastGroup? left, AerodromeForecastGroup? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="AerodromeForecastGroup"/> operands differ by value.</summary>
        public static bool operator !=(AerodromeForecastGroup? left, AerodromeForecastGroup? right) => !Equals(left, right);
    }
}
