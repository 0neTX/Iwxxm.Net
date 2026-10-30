using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Enumeration of all aeronautical meteorological report types supported by the ICAO/WMO IWXXM 2025-2 suite
    /// and SWIM Collect 2014 envelopes.
    /// </summary>
    public enum IwxxmReportType
    {
        SignificantWeather = 1,
        Metar = 2,
        Speci = 3,
        Taf = 4,
        Sigmet = 5,
        VolcanicAshSigmet = 6,
        TropicalCycloneSigmet = 7,
        Airmet = 8,
        VolcanoObservatoryNotice = 9,
        QuantitativeVolcanicAshConcentration = 10,
        VolcanicAshAdvisory = 11,
        TropicalCycloneAdvisory = 12,
        SpaceWeatherAdvisory = 13,
        MeteorologicalBulletin = 14
    }
}
