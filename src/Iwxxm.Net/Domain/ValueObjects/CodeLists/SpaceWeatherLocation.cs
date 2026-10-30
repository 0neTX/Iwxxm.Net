namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Space weather location indicator (WMO code list <c>http://codes.wmo.int/49-2/SpaceWxLocation</c>, codes valid for IWXXM 2025-2).
    /// Members are listed with their official notation (<c>skos:notation</c>); the documentation quotes or summarizes the official
    /// description. <see cref="Unknown"/> (the default value) means the reported value is not an official code. <c>DAYLIGHT_SIDE</c> is published in the register but is not valid for IWXXM 2025-2, so it resolves to <c>Unknown</c>.
    /// </summary>
    /// <remarks>Used through <see cref="CodeListValue{TCode}"/>, which also keeps the code as reported.</remarks>
    public enum SpaceWeatherLocation
    {
        /// <summary>The reported value is not a code of the official list valid for IWXXM 2025-2.</summary>
        Unknown = 0,
        /// <summary><c>DAYSIDE</c> — Day side.</summary>
        Dayside,
        /// <summary><c>NIGHTSIDE</c> — Night side.</summary>
        Nightside,
        /// <summary><c>HNH</c> — High latitudes northern hemisphere (90 degrees N - 60 degrees N).</summary>
        HighLatitudesNorth,
        /// <summary><c>MNH</c> — Middle latitudes northern hemisphere (60 degrees N - 30 degrees N).</summary>
        MiddleLatitudesNorth,
        /// <summary><c>EQN</c> — Equatorial latitudes northern hemisphere (30 degrees N - 0 degrees N).</summary>
        EquatorialLatitudesNorth,
        /// <summary><c>EQS</c> — Equatorial latitudes southern hemisphere (0 degrees S - 30 degrees S).</summary>
        EquatorialLatitudesSouth,
        /// <summary><c>MSH</c> — Middle latitudes southern hemisphere (30 degrees S - 60 degrees S).</summary>
        MiddleLatitudesSouth,
        /// <summary><c>HSH</c> — High latitudes southern hemisphere (60 degrees S - 90 degrees S).</summary>
        HighLatitudesSouth
    }
}
