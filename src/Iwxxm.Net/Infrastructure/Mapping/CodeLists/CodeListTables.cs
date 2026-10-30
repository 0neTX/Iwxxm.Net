using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping.CodeLists
{
    /// <summary>
    /// Translation tables of the closed WMO code lists referenced by IWXXM 2025-2 reports (Anti-Corruption Layer).
    /// Each table maps the official codes valid for IWXXM 2025-2 to a Domain enumeration; <c>CodeListTranslationTests</c>
    /// checks every table against the embedded official RDF file, which remains the source of truth.
    /// </summary>
    internal static class CodeListTables
    {
        private const string ResourceFolder = "Codelists/";

        /// <summary>WMO code list <c>http://codes.wmo.int/49-2/CloudAmountReportedAtAerodrome</c> (<see cref="AerodromeCloudAmount"/>).</summary>
        internal static readonly CodeListTable<AerodromeCloudAmount> AerodromeCloudAmounts = new CodeListTable<AerodromeCloudAmount>(
            "http://codes.wmo.int/49-2/CloudAmountReportedAtAerodrome",
            ResourceFolder + "codes.wmo.int-49-2-CloudAmountReportedAtAerodrome.rdf",
            new Dictionary<string, AerodromeCloudAmount>
            {
                ["SKC"] = AerodromeCloudAmount.SkyClear,
                ["FEW"] = AerodromeCloudAmount.Few,
                ["SCT"] = AerodromeCloudAmount.Scattered,
                ["BKN"] = AerodromeCloudAmount.Broken,
                ["OVC"] = AerodromeCloudAmount.Overcast,
                ["ISOL"] = AerodromeCloudAmount.Isolated,
                ["OCNL"] = AerodromeCloudAmount.Occasional,
                ["FRQ"] = AerodromeCloudAmount.Frequent,
                ["EMBD"] = AerodromeCloudAmount.Embedded,
                ["LYR"] = AerodromeCloudAmount.Layers
            });

        /// <summary>WMO code list <c>http://codes.wmo.int/49-2/SigConvectiveCloudType</c> (<see cref="ConvectiveCloudType"/>).</summary>
        internal static readonly CodeListTable<ConvectiveCloudType> ConvectiveCloudTypes = new CodeListTable<ConvectiveCloudType>(
            "http://codes.wmo.int/49-2/SigConvectiveCloudType",
            ResourceFolder + "codes.wmo.int-49-2-SigConvectiveCloudType.rdf",
            new Dictionary<string, ConvectiveCloudType>
            {
                ["CB"] = ConvectiveCloudType.Cumulonimbus,
                ["TCU"] = ConvectiveCloudType.ToweringCumulus
            });

        /// <summary>WMO code list <c>http://codes.wmo.int/49-2/SigWxPhenomena</c> (<see cref="SigmetPhenomenon"/>).</summary>
        internal static readonly CodeListTable<SigmetPhenomenon> SigmetPhenomena = new CodeListTable<SigmetPhenomenon>(
            "http://codes.wmo.int/49-2/SigWxPhenomena",
            ResourceFolder + "codes.wmo.int-49-2-SigWxPhenomena.rdf",
            new Dictionary<string, SigmetPhenomenon>
            {
                ["OBSC_TS"] = SigmetPhenomenon.ObscuredThunderstorm,
                ["OBSC_TSGR"] = SigmetPhenomenon.ObscuredThunderstormWithHail,
                ["EMBD_TS"] = SigmetPhenomenon.EmbeddedThunderstorm,
                ["EMBD_TSGR"] = SigmetPhenomenon.EmbeddedThunderstormWithHail,
                ["FRQ_TS"] = SigmetPhenomenon.FrequentThunderstorm,
                ["FRQ_TSGR"] = SigmetPhenomenon.FrequentThunderstormWithHail,
                ["SQL_TS"] = SigmetPhenomenon.SquallLine,
                ["SQL_TSGR"] = SigmetPhenomenon.SquallLineWithHail,
                ["TC"] = SigmetPhenomenon.TropicalCyclone,
                ["SEV_TURB"] = SigmetPhenomenon.SevereTurbulence,
                ["SEV_ICE"] = SigmetPhenomenon.SevereIcing,
                ["SEV_ICE_FZRA"] = SigmetPhenomenon.SevereIcingFreezingRain,
                ["SEV_MTW"] = SigmetPhenomenon.SevereMountainWave,
                ["HVY_DS"] = SigmetPhenomenon.HeavyDustStorm,
                ["HVY_SS"] = SigmetPhenomenon.HeavySandStorm,
                ["VA"] = SigmetPhenomenon.VolcanicAsh,
                ["RDOACT_CLD"] = SigmetPhenomenon.RadioactiveCloud
            });

        /// <summary>WMO code list <c>http://codes.wmo.int/49-2/AirWxPhenomena</c> (<see cref="AirmetPhenomenon"/>).</summary>
        internal static readonly CodeListTable<AirmetPhenomenon> AirmetPhenomena = new CodeListTable<AirmetPhenomenon>(
            "http://codes.wmo.int/49-2/AirWxPhenomena",
            ResourceFolder + "codes.wmo.int-49-2-AirWxPhenomena.rdf",
            new Dictionary<string, AirmetPhenomenon>
            {
                ["SFC_WIND"] = AirmetPhenomenon.SurfaceWind,
                ["SFC_VIS"] = AirmetPhenomenon.SurfaceVisibility,
                ["ISOL_TS"] = AirmetPhenomenon.IsolatedThunderstorms,
                ["ISOL_TSGR"] = AirmetPhenomenon.IsolatedThunderstormsWithHail,
                ["OCNL_TS"] = AirmetPhenomenon.OccasionalThunderstorms,
                ["OCNL_TSGR"] = AirmetPhenomenon.OccasionalThunderstormsWithHail,
                ["MT_OBSC"] = AirmetPhenomenon.MountainObscuration,
                ["BKN_CLD"] = AirmetPhenomenon.BrokenCloud,
                ["OVC_CLD"] = AirmetPhenomenon.OvercastCloud,
                ["ISOL_CB"] = AirmetPhenomenon.IsolatedCumulonimbus,
                ["OCNL_CB"] = AirmetPhenomenon.OccasionalCumulonimbus,
                ["FRQ_CB"] = AirmetPhenomenon.FrequentCumulonimbus,
                ["ISOL_TCU"] = AirmetPhenomenon.IsolatedToweringCumulus,
                ["OCNL_TCU"] = AirmetPhenomenon.OccasionalToweringCumulus,
                ["FRQ_TCU"] = AirmetPhenomenon.FrequentToweringCumulus,
                ["MOD_TURB"] = AirmetPhenomenon.ModerateTurbulence,
                ["MOD_ICE"] = AirmetPhenomenon.ModerateIcing,
                ["MOD_MTW"] = AirmetPhenomenon.ModerateMountainWave
            });

        /// <summary>WMO code list <c>http://codes.wmo.int/49-2/WeatherCausingVisibilityReduction</c> (<see cref="VisibilityReductionCause"/>).</summary>
        internal static readonly CodeListTable<VisibilityReductionCause> VisibilityReductionCauses = new CodeListTable<VisibilityReductionCause>(
            "http://codes.wmo.int/49-2/WeatherCausingVisibilityReduction",
            ResourceFolder + "codes.wmo.int-49-2-WeatherCausingVisibilityReduction.rdf",
            new Dictionary<string, VisibilityReductionCause>
            {
                ["BR"] = VisibilityReductionCause.Mist,
                ["DS"] = VisibilityReductionCause.Duststorm,
                ["DU"] = VisibilityReductionCause.Dust,
                ["DZ"] = VisibilityReductionCause.Drizzle,
                ["FC"] = VisibilityReductionCause.FunnelCloud,
                ["FG"] = VisibilityReductionCause.Fog,
                ["FU"] = VisibilityReductionCause.Smoke,
                ["GR"] = VisibilityReductionCause.Hail,
                ["GS"] = VisibilityReductionCause.SmallHail,
                ["HZ"] = VisibilityReductionCause.Haze,
                ["PL"] = VisibilityReductionCause.IcePellets,
                ["PO"] = VisibilityReductionCause.DustSandWhirls,
                ["RA"] = VisibilityReductionCause.Rain,
                ["SA"] = VisibilityReductionCause.Sand,
                ["SG"] = VisibilityReductionCause.SnowGrains,
                ["SN"] = VisibilityReductionCause.Snow,
                ["SQ"] = VisibilityReductionCause.Squall,
                ["SS"] = VisibilityReductionCause.Sandstorm,
                ["VA"] = VisibilityReductionCause.VolcanicAsh
            });

        /// <summary>WMO code list <c>http://codes.wmo.int/iwxxm/AviationColourCode</c> (<see cref="AviationColourCode"/>).</summary>
        internal static readonly CodeListTable<AviationColourCode> AviationColourCodes = new CodeListTable<AviationColourCode>(
            "http://codes.wmo.int/iwxxm/AviationColourCode",
            ResourceFolder + "codes.wmo.int-iwxxm-AviationColourCode.rdf",
            new Dictionary<string, AviationColourCode>
            {
                ["GREEN"] = AviationColourCode.Green,
                ["YELLOW"] = AviationColourCode.Yellow,
                ["ORANGE"] = AviationColourCode.Orange,
                ["RED"] = AviationColourCode.Red,
                ["UNASSIGNED"] = AviationColourCode.Unassigned
            });

        /// <summary>WMO code list <c>http://codes.wmo.int/49-2/SpaceWxLocation</c> (<see cref="SpaceWeatherLocation"/>).</summary>
        internal static readonly CodeListTable<SpaceWeatherLocation> SpaceWeatherLocations = new CodeListTable<SpaceWeatherLocation>(
            "http://codes.wmo.int/49-2/SpaceWxLocation",
            ResourceFolder + "codes.wmo.int-49-2-SpaceWxLocation.rdf",
            new Dictionary<string, SpaceWeatherLocation>
            {
                ["DAYSIDE"] = SpaceWeatherLocation.Dayside,
                ["NIGHTSIDE"] = SpaceWeatherLocation.Nightside,
                ["HNH"] = SpaceWeatherLocation.HighLatitudesNorth,
                ["MNH"] = SpaceWeatherLocation.MiddleLatitudesNorth,
                ["EQN"] = SpaceWeatherLocation.EquatorialLatitudesNorth,
                ["EQS"] = SpaceWeatherLocation.EquatorialLatitudesSouth,
                ["MSH"] = SpaceWeatherLocation.MiddleLatitudesSouth,
                ["HSH"] = SpaceWeatherLocation.HighLatitudesSouth
            });
    }
}
