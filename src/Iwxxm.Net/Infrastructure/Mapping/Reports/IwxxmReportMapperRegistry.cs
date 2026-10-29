using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>
    /// Static registry of the per-family report mappers, keyed by the qualified name of the IWXXM 2025-2 root element.
    /// </summary>
    /// <remarks>
    /// IWXXM 2025-2 defines a closed catalog of report types, so the registry is a plain static table (no reflection or
    /// plug-in discovery). SIGWX and COLLECT envelopes are handled by the composite mapper because they need the SIGWX
    /// outbound port and recursive dispatch.
    /// </remarks>
    internal static class IwxxmReportMapperRegistry
    {
        private static readonly Dictionary<XName, Func<XElement, Result<IIwxxmReport>>> Mappers =
            new Dictionary<XName, Func<XElement, Result<IIwxxmReport>>>
            {
                [IwxxmNs + "METAR"] = root => Upcast(MetarSpeciMapper.Map(root, IwxxmReportType.Metar)),
                [IwxxmNs + "SPECI"] = root => Upcast(MetarSpeciMapper.Map(root, IwxxmReportType.Speci)),
                [IwxxmNs + "TAF"] = root => Upcast(TafMapper.Map(root)),
                [IwxxmNs + "SIGMET"] = root => Upcast(SigmetMapper.Map(root, IwxxmReportType.Sigmet)),
                [IwxxmNs + "VolcanicAshSIGMET"] = root => Upcast(SigmetMapper.Map(root, IwxxmReportType.VolcanicAshSigmet)),
                [IwxxmNs + "TropicalCycloneSIGMET"] = root => Upcast(SigmetMapper.Map(root, IwxxmReportType.TropicalCycloneSigmet)),
                [IwxxmNs + "AIRMET"] = root => Upcast(AirmetMapper.Map(root)),
                [IwxxmNs + "VolcanoObservatoryNoticeForAviation"] = root => Upcast(VonaMapper.Map(root)),
                [IwxxmNs + "QuantitativeVolcanicAshConcentrationInformation"] = root => Upcast(QvaciMapper.Map(root)),
                [IwxxmNs + "VolcanicAshAdvisory"] = root => Upcast(VolcanicAshAdvisoryMapper.Map(root)),
                [IwxxmNs + "TropicalCycloneAdvisory"] = root => Upcast(TropicalCycloneAdvisoryMapper.Map(root)),
                [IwxxmNs + "SpaceWeatherAdvisory"] = root => Upcast(SpaceWeatherAdvisoryMapper.Map(root))
            };

        /// <summary>Finds the mapper registered for the root element name.</summary>
        public static bool TryGetMapper(XName rootName, out Func<XElement, Result<IIwxxmReport>> mapper) =>
            Mappers.TryGetValue(rootName, out mapper!);

        /// <summary>Converts a typed mapping result into the polymorphic <see cref="IIwxxmReport"/> result.</summary>
        public static Result<IIwxxmReport> Upcast<TReport>(Result<TReport> typedResult)
            where TReport : class, IIwxxmReport
        {
            return typedResult.IsSuccess
                ? Result<IIwxxmReport>.Success(typedResult.Value)
                : Result<IIwxxmReport>.Failure(typedResult.Errors);
        }
    }
}
