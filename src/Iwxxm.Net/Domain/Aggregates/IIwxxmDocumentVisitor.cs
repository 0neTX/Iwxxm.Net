namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Visitor over the closed catalog of mapped documents: the IWXXM 2025-2 report aggregates and the SWIM
    /// <c>collect:MeteorologicalBulletin</c> envelope (GoF Visitor, double dispatch through
    /// <see cref="IwxxmDocument.Accept{TResult}"/>).
    /// </summary>
    /// <typeparam name="TResult">Type of the value produced for each visited report.</typeparam>
    /// <remarks>
    /// Replaces runtime type switches (<c>switch (report) { case Taf ... }</c>): when a report type is added,
    /// every visitor stops compiling until it handles the new type. <see cref="IwxxmReportType"/> remains available
    /// as a descriptive classification (for example, it distinguishes METAR from SPECI and the three SIGMET kinds).
    /// </remarks>
    public interface IIwxxmDocumentVisitor<out TResult>
    {
        /// <summary>Visits a METAR aerodrome routine observation.</summary>
        TResult Visit(Metar metar);

        /// <summary>Visits a SPECI aerodrome special observation.</summary>
        TResult Visit(Speci speci);

        /// <summary>Visits a TAF aerodrome forecast.</summary>
        TResult Visit(Taf report);

        /// <summary>Visits a SIGMET (including volcanic ash and tropical cyclone SIGMETs).</summary>
        TResult Visit(Sigmet report);

        /// <summary>Visits an AIRMET.</summary>
        TResult Visit(Airmet report);

        /// <summary>Visits a Volcano Observatory Notice for Aviation (VONA).</summary>
        TResult Visit(Vona report);

        /// <summary>Visits a Quantitative Volcanic Ash Concentration Information (QVACI) report.</summary>
        TResult Visit(Qvaci report);

        /// <summary>Visits a Volcanic Ash Advisory (VAA).</summary>
        TResult Visit(Vaa report);

        /// <summary>Visits a Tropical Cyclone Advisory (TCA).</summary>
        TResult Visit(Tca report);

        /// <summary>Visits a Space Weather Advisory.</summary>
        TResult Visit(SpaceWx report);

        /// <summary>Visits a WAFS significant weather (SIGWX) forecast.</summary>
        TResult Visit(Sigwx report);

        /// <summary>Visits a SWIM <c>collect:MeteorologicalBulletin</c> envelope.</summary>
        TResult Visit(MeteorologicalBulletin bulletin);
    }
}
