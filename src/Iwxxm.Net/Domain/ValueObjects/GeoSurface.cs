using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Closed union of the horizontal surfaces an IWXXM report can describe: a <see cref="GeoPolygon"/>
    /// (<c>gml:PolygonPatch</c> with <c>gml:LinearRing</c> / <c>gml:Ring</c>) or a <see cref="GeoCircle"/>
    /// (<c>gml:CircleByCenterPoint</c>, e.g. "WI 250NM OF TC CENTRE" or the space weather <c>DAYSIDE</c>).
    /// </summary>
    /// <remarks>
    /// The union is closed: its constructor is not accessible outside the library, so <see cref="GeoPolygon"/> and
    /// <see cref="GeoCircle"/> are the only cases. Two styles handle both. <see cref="Match{TResult}"/> and
    /// <see cref="Match(Action{GeoPolygon}, Action{GeoCircle})"/> take one handler per case, so the compiler forces you to
    /// supply both and a new case could never be forgotten. C# pattern matching works on the public sealed subtypes
    /// (<c>switch (area) { case GeoPolygon polygon: ...; case GeoCircle circle: ...; }</c>) and reads naturally in statements,
    /// but the compiler does not check that every case is covered.
    /// </remarks>
    public abstract class GeoSurface
    {
        internal GeoSurface()
        {
        }

        /// <summary>Applies the function that matches the concrete surface.</summary>
        public abstract TResult Match<TResult>(Func<GeoPolygon, TResult> polygon, Func<GeoCircle, TResult> circle);

        /// <summary>Runs the action that matches the concrete surface (the statement form of <see cref="Match{TResult}"/>).</summary>
        /// <exception cref="ArgumentNullException"><paramref name="polygon"/> or <paramref name="circle"/> is <c>null</c>.</exception>
        public void Match(Action<GeoPolygon> polygon, Action<GeoCircle> circle)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));
            if (circle == null)
                throw new ArgumentNullException(nameof(circle));

            Match<bool>(
                p =>
                {
                    polygon(p);
                    return true;
                },
                c =>
                {
                    circle(c);
                    return true;
                });
        }
    }
}
