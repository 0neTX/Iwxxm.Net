using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing a quantitative volcanic ash concentration cloud contour
    /// inside a <see cref="Qvaci"/> (<c>QuantitativeVolcanicAshConcentrationInformation</c>).
    /// <see cref="AshDensityMgPerM3"/> is the reported <c>ashDensityEqualsOrExceeds</c> (mandatory in <c>qvaci.xsd</c>);
    /// vertical limits keep the reported value, unit and reference.
    /// </summary>
    public sealed class VolcanicAshConcentrationCloud : IEquatable<VolcanicAshConcentrationCloud>
    {
        public string Id { get; }
        public GeoPolygon Area { get; }
        public VerticalLimit? LowerLimit { get; }
        public VerticalLimit? UpperLimit { get; }
        public double AshDensityMgPerM3 { get; }

        public VolcanicAshConcentrationCloud(
            string id,
            GeoPolygon area,
            VerticalLimit? lowerLimit,
            VerticalLimit? upperLimit,
            double ashDensityMgPerM3)
        {
            Id = id ?? string.Empty;
            Area = area ?? throw new ArgumentNullException(nameof(area));
            LowerLimit = lowerLimit;
            UpperLimit = upperLimit;
            AshDensityMgPerM3 = ashDensityMgPerM3;
        }

        public bool Equals(VolcanicAshConcentrationCloud? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                   Area.Equals(other.Area) &&
                   Nullable.Equals(LowerLimit, other.LowerLimit) &&
                   Nullable.Equals(UpperLimit, other.UpperLimit) &&
                   AshDensityMgPerM3.Equals(other.AshDensityMgPerM3);
        }

        public override bool Equals(object? obj) => obj is VolcanicAshConcentrationCloud other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Seed;
            hash = ValueEquality.Add(hash, Id);
            hash = ValueEquality.Add(hash, Area);
            hash = ValueEquality.Add(hash, LowerLimit);
            hash = ValueEquality.Add(hash, UpperLimit);
            hash = ValueEquality.AddDouble(hash, AshDensityMgPerM3);
            return hash;
        }

        public static bool operator ==(VolcanicAshConcentrationCloud? left, VolcanicAshConcentrationCloud? right) => Equals(left, right);

        public static bool operator !=(VolcanicAshConcentrationCloud? left, VolcanicAshConcentrationCloud? right) => !Equals(left, right);
    }
}
