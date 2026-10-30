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
        private static readonly Dictionary<XName, Func<XElement, Result<IwxxmReport>>> Mappers =
            new Dictionary<XName, Func<XElement, Result<IwxxmReport>>>
            {
                [IwxxmNs + "METAR"] = root => Upcast(MetarSpeciMapper.Map<Metar>(root, Metar.Create)),
                [IwxxmNs + "SPECI"] = root => Upcast(MetarSpeciMapper.Map<Speci>(root, Speci.Create)),
                [IwxxmNs + "TAF"] = root => Upcast(TafMapper.Map(root)),
                [IwxxmNs + "SIGMET"] = root => Upcast(SigmetMapper.Map(root, IwxxmReportType.Sigmet)),
                [IwxxmNs + "VolcanicAshSIGMET"] = root => Upcast(SigmetMapper.Map(root, IwxxmReportType.VolcanicAshSigmet)),
                [IwxxmNs + "TropicalCycloneSIGMET"] = root => Upcast(SigmetMapper.Map(root, IwxxmReportType.TropicalCycloneSigmet)),
                [IwxxmNs + "AIRMET"] = root => Upcast(AirmetMapper.Map(root)),
                [IwxxmNs + "VolcanoObservatoryNoticeForAviation"] = root => Upcast(VonaMapper.Map(root)),
                [IwxxmNs + "QuantitativeVolcanicAshConcentrationInformation"] = root => Upcast(QvaciMapper.Map(root)),
                [IwxxmNs + "VolcanicAshAdvisory"] = root => Upcast(VaaMapper.Map(root)),
                [IwxxmNs + "TropicalCycloneAdvisory"] = root => Upcast(TcaMapper.Map(root)),
                [IwxxmNs + "SpaceWeatherAdvisory"] = root => Upcast(SpaceWxMapper.Map(root))
            };

        /// <summary>Finds the mapper registered for the root element name.</summary>
        public static bool TryGetMapper(XName rootName, out Func<XElement, Result<IwxxmReport>> mapper) =>
            Mappers.TryGetValue(rootName, out mapper!);

        /// <summary>Converts a typed report mapping result into the polymorphic <see cref="IwxxmReport"/> result.</summary>
        public static Result<IwxxmReport> Upcast<TReport>(Result<TReport> typedResult)
            where TReport : IwxxmReport
        {
            return typedResult.IsSuccess
                ? Result<IwxxmReport>.Success(typedResult.Value)
                : Result<IwxxmReport>.Failure(typedResult.Errors);
        }

        /// <summary>Converts a typed mapping result (report or bulletin) into the polymorphic <see cref="IwxxmDocument"/> result.</summary>
        public static Result<IwxxmDocument> ToDocument<TDocument>(Result<TDocument> typedResult)
            where TDocument : IwxxmDocument
        {
            return typedResult.IsSuccess
                ? Result<IwxxmDocument>.Success(typedResult.Value)
                : Result<IwxxmDocument>.Failure(typedResult.Errors);
        }
    }
}
