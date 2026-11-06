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
        private static readonly Dictionary<XName, Func<XElement, UomAliasCollector, Result<IwxxmReport>>> Mappers =
            new Dictionary<XName, Func<XElement, UomAliasCollector, Result<IwxxmReport>>>
            {
                [IwxxmNs + "METAR"] = (root, aliases) => Upcast(MetarSpeciMapper.Map<Metar>(root, Metar.Create, aliases)),
                [IwxxmNs + "SPECI"] = (root, aliases) => Upcast(MetarSpeciMapper.Map<Speci>(root, Speci.Create, aliases)),
                [IwxxmNs + "TAF"] = (root, aliases) => Upcast(TafMapper.Map(root, aliases)),
                [IwxxmNs + "SIGMET"] = (root, aliases) => Upcast(SigmetMapper.Map(root, IwxxmReportType.Sigmet, aliases)),
                [IwxxmNs + "VolcanicAshSIGMET"] = (root, aliases) => Upcast(SigmetMapper.Map(root, IwxxmReportType.VolcanicAshSigmet, aliases)),
                [IwxxmNs + "TropicalCycloneSIGMET"] = (root, aliases) => Upcast(SigmetMapper.Map(root, IwxxmReportType.TropicalCycloneSigmet, aliases)),
                [IwxxmNs + "AIRMET"] = (root, aliases) => Upcast(AirmetMapper.Map(root, aliases)),
                [IwxxmNs + "VolcanoObservatoryNoticeForAviation"] = (root, _) => Upcast(VonaMapper.Map(root)),
                [IwxxmNs + "QuantitativeVolcanicAshConcentrationInformation"] = (root, _) => Upcast(QvaciMapper.Map(root)),
                [IwxxmNs + "VolcanicAshAdvisory"] = (root, aliases) => Upcast(VaaMapper.Map(root, aliases)),
                [IwxxmNs + "TropicalCycloneAdvisory"] = (root, aliases) => Upcast(TcaMapper.Map(root, aliases)),
                [IwxxmNs + "SpaceWeatherAdvisory"] = (root, _) => Upcast(SpaceWxMapper.Map(root))
            };

        /// <summary>Finds the mapper registered for the root element name.</summary>
        public static bool TryGetMapper(XName rootName, out Func<XElement, UomAliasCollector, Result<IwxxmReport>> mapper) =>
            Mappers.TryGetValue(rootName, out mapper!);

        /// <summary>Converts a typed report mapping result into the polymorphic <see cref="IwxxmReport"/> result.</summary>
        public static Result<IwxxmReport> Upcast<TReport>(Result<TReport> typedResult)
            where TReport : IwxxmReport
        {
            return typedResult.IsSuccess
                ? Result<IwxxmReport>.Success(typedResult.Value, typedResult.Warnings)
                : Result<IwxxmReport>.Failure(typedResult.Errors);
        }

        /// <summary>Converts a typed mapping result (report or bulletin) into the polymorphic <see cref="IwxxmDocument"/> result.</summary>
        public static Result<IwxxmDocument> ToDocument<TDocument>(Result<TDocument> typedResult)
            where TDocument : IwxxmDocument
        {
            return typedResult.IsSuccess
                ? Result<IwxxmDocument>.Success(typedResult.Value, typedResult.Warnings)
                : Result<IwxxmDocument>.Failure(typedResult.Errors);
        }
    }
}
