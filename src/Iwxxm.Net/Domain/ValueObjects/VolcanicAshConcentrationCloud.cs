using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing a quantitative volcanic ash concentration cloud contour
    /// inside a <see cref="Qvaci"/> (<c>QuantitativeVolcanicAshConcentrationInformation</c>).
    /// <see cref="AshDensityEqualsOrExceedsMgPerM3"/> is the reported <c>ashDensityEqualsOrExceeds</c> (mandatory in <c>qvaci.xsd</c>), a lower bound: the ash density within the cloud equals or exceeds it;
    /// vertical limits keep the reported value, unit and reference.
    /// </summary>
    public sealed class VolcanicAshConcentrationCloud : IEquatable<VolcanicAshConcentrationCloud>
    {
        /// <summary>Gets the <c>gml:id</c> of the feature.</summary>
        public string Id { get; }
        /// <summary>Gets the horizontal extent of the modelled ash concentration area.</summary>
        public GeoPolygon Area { get; }
        /// <summary>Gets the lower vertical limit of the ash cloud (<c>lowerElevation</c> with its vertical reference) as reported, or <c>null</c> when not reported.</summary>
        public VerticalLimit? LowerLimit { get; }
        /// <summary>Gets the upper vertical limit of the ash cloud (<c>upperElevation</c> with its vertical reference) as reported, or <c>null</c> when not reported.</summary>
        public VerticalLimit? UpperLimit { get; }
        /// <summary>Gets the lower bound of the ash density within the cloud (<c>iwxxm:ashDensityEqualsOrExceeds</c>, "equals or exceeds") in milligrams per cubic metre; it is a threshold, not the density itself: higher values are possible within the cloud. the mapper rejects a cloud without it and accepts no other unit.</summary>
        public double AshDensityEqualsOrExceedsMgPerM3 { get; }

        /// <summary>
        /// <c>phenomenonTime</c> of the ash cloud feature (inline or by local <c>xlink:href="#id"</c>, e.g. the shared
        /// forecast time of the official QVACI example); <c>null</c> when absent, nil or unresolved.
        /// </summary>
        public DateTimeOffset? PhenomenonTimeUtc { get; }

        /// <summary>Creates an ash concentration cloud.</summary>
        /// <param name="id">The <c>gml:id</c> of the ash cloud feature; <c>null</c> is stored as an empty string.</param>
        /// <param name="area">The horizontal extent; required.</param>
        /// <param name="lowerLimit">The lower vertical limit; <c>null</c> when not reported.</param>
        /// <param name="upperLimit">The upper vertical limit; <c>null</c> when not reported.</param>
        /// <param name="ashDensityEqualsOrExceedsMgPerM3">The lower bound of the ash density in milligrams per cubic metre (the density equals or exceeds it; higher values are possible).</param>
        /// <param name="phenomenonTimeUtc">The time the cloud applies to; <c>null</c> when absent, nil or unresolved.</param>
        /// <exception cref="ArgumentNullException"><paramref name="area"/> is <c>null</c>.</exception>
        public VolcanicAshConcentrationCloud(
            string id,
            GeoPolygon area,
            VerticalLimit? lowerLimit,
            VerticalLimit? upperLimit,
            double ashDensityEqualsOrExceedsMgPerM3,
            DateTimeOffset? phenomenonTimeUtc = null)
        {
            Id = id ?? string.Empty;
            Area = area ?? throw new ArgumentNullException(nameof(area));
            LowerLimit = lowerLimit;
            UpperLimit = upperLimit;
            AshDensityEqualsOrExceedsMgPerM3 = ashDensityEqualsOrExceedsMgPerM3;
            PhenomenonTimeUtc = phenomenonTimeUtc;
        }

        /// <inheritdoc />
        public bool Equals(VolcanicAshConcentrationCloud? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                   Area.Equals(other.Area) &&
                   Nullable.Equals(LowerLimit, other.LowerLimit) &&
                   Nullable.Equals(UpperLimit, other.UpperLimit) &&
                   AshDensityEqualsOrExceedsMgPerM3.Equals(other.AshDensityEqualsOrExceedsMgPerM3) &&
                   Nullable.Equals(PhenomenonTimeUtc, other.PhenomenonTimeUtc);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is VolcanicAshConcentrationCloud other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = ValueEquality.Seed;
            hash = ValueEquality.Add(hash, Id);
            hash = ValueEquality.Add(hash, Area);
            hash = ValueEquality.Add(hash, LowerLimit);
            hash = ValueEquality.Add(hash, UpperLimit);
            hash = ValueEquality.AddDouble(hash, AshDensityEqualsOrExceedsMgPerM3);
            return ValueEquality.Add(hash, PhenomenonTimeUtc);
        }

        /// <summary>Returns <c>true</c> when the two <see cref="VolcanicAshConcentrationCloud"/> operands are equal by value.</summary>
        public static bool operator ==(VolcanicAshConcentrationCloud? left, VolcanicAshConcentrationCloud? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="VolcanicAshConcentrationCloud"/> operands differ by value.</summary>
        public static bool operator !=(VolcanicAshConcentrationCloud? left, VolcanicAshConcentrationCloud? right) => !Equals(left, right);
    }
}
