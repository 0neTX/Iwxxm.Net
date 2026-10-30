namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Aviation colour code of a volcano (WMO code list <c>http://codes.wmo.int/iwxxm/AviationColourCode</c>, codes valid for IWXXM 2025-2).
    /// Members are listed with their official notation (<c>skos:notation</c>); the documentation quotes or summarizes the official
    /// description. <see cref="Unknown"/> (the default value) means the reported value is not an official code. The register <c>http://codes.wmo.int/49-2/AviationColourCode</c> is not valid for IWXXM 2025-2 (no 2025-2 version), so its references resolve to <c>Unknown</c>.
    /// </summary>
    /// <remarks>Used through <see cref="CodeListValue{TCode}"/>, which also keeps the code as reported.</remarks>
    public enum AviationColourCode
    {
        /// <summary>The reported value is not a code of the official list valid for IWXXM 2025-2.</summary>
        Unknown = 0,
        /// <summary><c>GREEN</c> — Volcano is in normal, non-eruptive state (or activity has ceased after a higher alert level).</summary>
        Green,
        /// <summary><c>YELLOW</c> — Volcano is experiencing signs of elevated unrest above known background levels.</summary>
        Yellow,
        /// <summary><c>ORANGE</c> — Volcano is exhibiting heightened unrest with increased likelihood of eruption, or an eruption with no or minor ash emission is under way.</summary>
        Orange,
        /// <summary><c>RED</c> — Eruption is forecast to be imminent with significant ash emission likely, or is under way with significant ash emission.</summary>
        Red,
        /// <summary><c>UNASSIGNED</c> — Not assigned.</summary>
        Unassigned
    }
}
