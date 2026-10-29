using System;
using System.IO;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net
{
    /// <summary>
    /// Public facade for validating and parsing ICAO/WMO IWXXM 2025-2 Significant Weather (SIGWX) documents
    /// (<c>iwxxm:WAFSSignificantWeatherForecast</c> and the non-standard compact <c>iwxxm:SIGWXReport</c>).
    /// </summary>
    /// <remarks>
    /// Instances are created by <see cref="IwxxmParserBuilder.BuildSigwxParser"/> and depend only on the inbound port
    /// <see cref="IIwxxmSigwxProcessor"/>; they are immutable and thread-safe. The static members are convenience wrappers
    /// over <see cref="Default"/>.
    /// <para>I/O and adapter failures are returned as typed errors (<c>FILE-NOT-FOUND</c>, <c>ACCESS-DENIED</c>,
    /// <c>FILE-READ-ERROR</c>, <c>SCHEMA-VALIDATOR-ERROR</c>, <c>RULE-VALIDATOR-ERROR</c>, <c>MAPPER-ERROR</c>); only a
    /// <c>null</c> stream (<see cref="ArgumentNullException"/>) or a <c>null</c>/whitespace path
    /// (<see cref="ArgumentException"/>) throws.</para>
    /// </remarks>
    public sealed class IwxxmSigwxParser : IIwxxmSigwxProcessor
    {
        private static readonly Lazy<IwxxmSigwxParser> DefaultInstance =
            new Lazy<IwxxmSigwxParser>(() => new IwxxmParserBuilder().BuildSigwxParser(), isThreadSafe: true);

        private readonly IIwxxmSigwxProcessor _processor;

        /// <summary>Initializes a facade over the given inbound port.</summary>
        public IwxxmSigwxParser(IIwxxmSigwxProcessor processor)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        }

        /// <summary>Gets the shared instance built with the default adapters and <see cref="IwxxmParserOptions.Default"/>.</summary>
        public static IwxxmSigwxParser Default => DefaultInstance.Value;

        /// <inheritdoc />
        public Result<SignificantWeatherReport> Process(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            return _processor.Process(xmlStream);
        }

        /// <summary>Validates and maps an XML string containing an IWXXM 2025-2 SIGWX document.</summary>
        public Result<SignificantWeatherReport> Process(string xmlContent) =>
            IwxxmParser.WithUtf8Stream(xmlContent, Process);

        /// <summary>Validates and maps an IWXXM 2025-2 SIGWX XML file from disk.</summary>
        public Result<SignificantWeatherReport> ProcessFile(string filePath) =>
            IwxxmParser.WithFileStream(filePath, "IWXXM file", Process);

        /// <summary>Validates and maps a SIGWX stream using <see cref="Default"/>.</summary>
        public static Result<SignificantWeatherReport> Parse(Stream xmlStream) => Default.Process(xmlStream);

        /// <summary>Validates and maps a SIGWX XML string using <see cref="Default"/>.</summary>
        public static Result<SignificantWeatherReport> Parse(string xmlContent) => Default.Process(xmlContent);

        /// <summary>Validates and maps a SIGWX XML file using <see cref="Default"/>.</summary>
        public static Result<SignificantWeatherReport> ParseFile(string filePath) => Default.ProcessFile(filePath);
    }
}
