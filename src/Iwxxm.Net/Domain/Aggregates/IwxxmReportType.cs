using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Enumeration of all aeronautical meteorological report types supported by the ICAO/WMO IWXXM 2025-2 suite
    /// and SWIM Collect 2014 envelopes.
    /// </summary>
    public enum IwxxmReportType
    {
        /// <summary>WAFS significant weather forecast (SIGWX, root element <c>iwxxm:WAFSSignificantWeatherForecast</c> of <c>WAFSSigWxFC.xsd</c>): high-level and medium-level significant weather features issued by a World Area Forecast Centre.</summary>
        SignificantWeather = 1,
        /// <summary>Aerodrome routine meteorological report (<c>iwxxm:METAR</c>), represented by <see cref="Metar"/>.</summary>
        Metar = 2,
        /// <summary>Aerodrome special meteorological report (<c>iwxxm:SPECI</c>), issued when conditions change significantly between routine reports; represented by <see cref="Speci"/>.</summary>
        Speci = 3,
        /// <summary>Terminal aerodrome forecast (<c>iwxxm:TAF</c>), represented by <see cref="Taf"/>.</summary>
        Taf = 4,
        /// <summary>SIGMET for en-route weather phenomena other than volcanic ash and tropical cyclones (<c>iwxxm:SIGMET</c>), represented by <see cref="Sigmet"/>.</summary>
        Sigmet = 5,
        /// <summary>SIGMET for volcanic ash (<c>iwxxm:VolcanicAshSIGMET</c>, a subclass of SIGMET that adds the volcano and forecast positions), represented by <see cref="Sigmet"/>.</summary>
        VolcanicAshSigmet = 6,
        /// <summary>SIGMET for a tropical cyclone (<c>iwxxm:TropicalCycloneSIGMET</c>, a subclass of SIGMET that adds the cyclone identification and forecast positions), represented by <see cref="Sigmet"/>.</summary>
        TropicalCycloneSigmet = 7,
        /// <summary>AIRMET for en-route weather phenomena that may affect the safety of low-level aircraft operations (<c>iwxxm:AIRMET</c>), represented by <see cref="Airmet"/>.</summary>
        Airmet = 8,
        /// <summary>Volcano observatory notice for aviation (VONA, <c>iwxxm:VolcanoObservatoryNoticeForAviation</c>) issued by a volcano observatory, represented by <see cref="Vona"/>.</summary>
        VolcanoObservatoryNotice = 9,
        /// <summary>Quantitative volcanic ash concentration information (QVA-CI, <c>iwxxm:QuantitativeVolcanicAshConcentrationInformation</c>) from a dispersion model, represented by <see cref="Qvaci"/>.</summary>
        QuantitativeVolcanicAshConcentration = 10,
        /// <summary>Volcanic ash advisory (VAA, <c>iwxxm:VolcanicAshAdvisory</c>) issued by a Volcanic Ash Advisory Centre, represented by <see cref="Vaa"/>.</summary>
        VolcanicAshAdvisory = 11,
        /// <summary>Tropical cyclone advisory (TCA, <c>iwxxm:TropicalCycloneAdvisory</c>) issued by a Tropical Cyclone Advisory Centre, represented by <see cref="Tca"/>.</summary>
        TropicalCycloneAdvisory = 12,
        /// <summary>Space weather advisory (<c>iwxxm:SpaceWeatherAdvisory</c>) issued by a space weather centre, represented by <see cref="SpaceWx"/>.</summary>
        SpaceWeatherAdvisory = 13,
        /// <summary>SWIM Collect bulletin (<c>collect:MeteorologicalBulletin</c>), an envelope that groups IWXXM reports; represented by <see cref="MeteorologicalBulletin"/>. It is not itself a report.</summary>
        MeteorologicalBulletin = 14
    }
}
