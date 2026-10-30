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
    /// <see cref="GeoCircle"/> are the only cases. Use <see cref="Match{TResult}"/> or C# pattern matching
    /// (<c>area is GeoPolygon polygon</c>) to handle both.
    /// </remarks>
    public abstract class GeoSurface
    {
        internal GeoSurface()
        {
        }

        /// <summary>Applies the function that matches the concrete surface.</summary>
        public abstract TResult Match<TResult>(Func<GeoPolygon, TResult> polygon, Func<GeoCircle, TResult> circle);
    }
}
