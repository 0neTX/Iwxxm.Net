using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping.Sigwx
{
    /// <summary>Accumulates the phenomena extracted from a SIGWX document, grouped by WAFS phenomenon family.</summary>
    internal sealed class SigwxFeatureSet
    {
        public List<JetStream> JetStreams { get; } = new List<JetStream>();
        public List<Cloud> Clouds { get; } = new List<Cloud>();
        public List<Tropopause> Tropopauses { get; } = new List<Tropopause>();
        public List<Volcano> Volcanoes { get; } = new List<Volcano>();
        public List<Turbulence> Turbulences { get; } = new List<Turbulence>();
        public List<AirframeIcing> AirframeIcings { get; } = new List<AirframeIcing>();
        public List<TropicalCyclone> TropicalCyclones { get; } = new List<TropicalCyclone>();
        public List<Sandstorm> Sandstorms { get; } = new List<Sandstorm>();
        public List<RadiationIncident> RadiationIncidents { get; } = new List<RadiationIncident>();

        /// <summary>Adds a successfully extracted phenomenon to <paramref name="target"/>, or its errors to <paramref name="errors"/>.</summary>
        public static void Collect<T>(Result<T> result, List<T> target, List<ValidationError> errors)
        {
            if (result.IsSuccess) target.Add(result.Value);
            else errors.AddRange(result.Errors);
        }
    }
}
