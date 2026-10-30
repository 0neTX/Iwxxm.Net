using System;
using System.Globalization;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Native geospatial Value Object representing a circle on the Earth surface, mapped from
    /// <c>gml:CircleByCenterPoint</c> (centre <c>gml:pos</c> plus <c>gml:radius</c> with its unit of measure).
    /// The radius keeps the reported value and unit (e.g. 250 <c>[nmi_i]</c>, 10100 <c>km</c>); no densification or
    /// unit conversion is applied in the Domain. Equality is structural and exact (centre and radius).
    /// </summary>
    public sealed class GeoCircle : GeoSurface, IEquatable<GeoCircle>
    {
        private GeoCircle(GeoCoordinate centre, Length radius)
        {
            Centre = centre;
            Radius = radius;
        }

        /// <summary>Centre of the circle (<c>gml:pos</c>).</summary>
        public GeoCoordinate Centre { get; }

        /// <summary>Radius as reported (<c>gml:radius</c> value and <c>@uom</c>).</summary>
        public Length Radius { get; }

        /// <summary>Creates a circle; the radius must be strictly positive.</summary>
        public static Result<GeoCircle> Create(GeoCoordinate centre, Length radius, string? location = null)
        {
            if (!(radius.Value > 0.0))
            {
                return Result<GeoCircle>.Failure(
                    "GEO-CIRCLE-RADIUS-INVALID",
                    string.Format(CultureInfo.InvariantCulture, "A circle radius must be strictly positive (received {0} {1}).", radius.Value, radius.UnitOfMeasure),
                    ErrorCategory.DomainMapping,
                    location);
            }

            return Result<GeoCircle>.Success(new GeoCircle(centre, radius));
        }

        /// <inheritdoc />
        public override TResult Match<TResult>(Func<GeoPolygon, TResult> polygon, Func<GeoCircle, TResult> circle) =>
            (circle ?? throw new ArgumentNullException(nameof(circle)))(this);

        public bool Equals(GeoCircle? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Centre.Equals(other.Centre) && Radius.Equals(other.Radius);
        }

        public override bool Equals(object? obj) => obj is GeoCircle other && Equals(other);

        public override int GetHashCode() => ValueEquality.Add(ValueEquality.Add(ValueEquality.Seed, Centre), Radius);

        public static bool operator ==(GeoCircle? left, GeoCircle? right) => Equals(left, right);

        public static bool operator !=(GeoCircle? left, GeoCircle? right) => !Equals(left, right);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "Circle({0}, {1} {2})", Centre, Radius.Value, Radius.UnitOfMeasure);
    }
}
