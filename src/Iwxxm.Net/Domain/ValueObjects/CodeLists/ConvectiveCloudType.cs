namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Significant convective cloud type (WMO code list <c>http://codes.wmo.int/49-2/SigConvectiveCloudType</c>, codes valid for IWXXM 2025-2).
    /// Members are listed with their official notation (<c>skos:notation</c>); the documentation quotes or summarizes the official
    /// description. <see cref="Unknown"/> (the default value) means the reported value is not an official code.
    /// </summary>
    /// <remarks>Used through <see cref="CodeListValue{TCode}"/>, which also keeps the code as reported.</remarks>
    public enum ConvectiveCloudType
    {
        /// <summary>The reported value is not a code of the official list valid for IWXXM 2025-2.</summary>
        Unknown = 0,
        /// <summary><c>CB</c> — Cumulonimbus.</summary>
        Cumulonimbus,
        /// <summary><c>TCU</c> — Towering cumulus.</summary>
        ToweringCumulus
    }
}
