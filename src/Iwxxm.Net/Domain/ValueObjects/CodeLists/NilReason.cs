namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Reason why a value is absent, from the official WMO register <c>http://codes.wmo.int/common/nil</c>
    /// (used as <c>CodeListValue&lt;NilReason&gt;</c>). The reason is what distinguishes, for example, "no weather of
    /// operational significance" (TAC <c>NSW</c>), "no cloud of operational significance" (<c>NSC</c>) or "no cloud
    /// detected by the automatic system" (<c>NCD</c>) from a value that is simply not reported (no element at all).
    /// </summary>
    public enum NilReason
    {
        /// <summary>The reported reason is not a code of the official register (kept with the reference as written).</summary>
        Unknown = 0,

        /// <summary><c>inapplicable</c>: there is no value (e.g. cloud base of a clear sky, TAC <c>SKC</c>/<c>CLR</c>).</summary>
        Inapplicable,

        /// <summary><c>missing</c>: the correct value is not readily available.</summary>
        Missing,

        /// <summary><c>template</c>: the value will be available later.</summary>
        Template,

        /// <summary><c>unknown</c>: the correct value is not known and not computable.</summary>
        ValueUnknown,

        /// <summary><c>withheld</c>: the value is not divulged.</summary>
        Withheld,

        /// <summary><c>nothingOfOperationalSignificance</c> (TAC <c>NSW</c> for weather, <c>NSC</c> for cloud).</summary>
        NothingOfOperationalSignificance,

        /// <summary><c>notDetectedByAutoSystem</c> (TAC <c>NCD</c> for cloud).</summary>
        NotDetectedByAutoSystem,

        /// <summary><c>notObservable</c>: a sensor or system failure prevented the observation (TAC <c>//</c>, <c>VV///</c>).</summary>
        NotObservable,

        /// <summary><c>noSignificantChange</c> (TAC <c>NOSIG</c>).</summary>
        NoSignificantChange,

        /// <summary><c>AboveDetectionRange</c>: the value is above the detection range of the instrument.</summary>
        AboveDetectionRange,

        /// <summary><c>BelowDetectionRange</c>: the value is below the detection range of the instrument.</summary>
        BelowDetectionRange
    }
}
