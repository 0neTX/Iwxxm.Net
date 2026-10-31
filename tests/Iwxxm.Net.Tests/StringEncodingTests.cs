using System.IO;
using Iwxxm.Net.Application.UseCases;
using Iwxxm.Net.Reports;
using Iwxxm.Net.Tests.DataFidelity;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// String and <see cref="TextReader"/> inputs are text: the encoding named by their XML declaration does not apply
    /// (AUD-M5). Previously the string was encoded to UTF-8 bytes and decoded with the declared encoding.
    /// </summary>
    public sealed class StringEncodingTests
    {
        private const string Utf8Declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";

        [Theory]
        [InlineData("UTF-16")]
        [InlineData("utf-16le")]
        [InlineData("ISO-8859-1")]
        [InlineData("windows-1252")]
        [InlineData("US-ASCII")]
        public void Parse_String_IgnoresTheDeclaredEncoding_AndKeepsNonAsciiCharacters(string declaredEncoding)
        {
            string xml = SampleDocuments.Text("metar-translation-failed.xml")
                .Replace(Utf8Declaration, Declaration(declaredEncoding))
                .Replace("METAR YUDO 221630Z INVALID", "METAR YUDO 221630Z ZÜRICH – ÅÉÎ €");

            Result<IwxxmDocument> result = IwxxmParser.Parse(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal("METAR YUDO 221630Z ZÜRICH – ÅÉÎ €", Assert.IsType<Metar>(result.Value).TranslationFailedTac);
        }

        [Fact]
        public void Process_TextReader_IgnoresTheDeclaredEncoding()
        {
            using (var reader = new StringReader(Metar("UTF-16")))
            {
                Result<IwxxmDocument> result = IwxxmParser.Default.Process(reader);
                Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            }
        }

        [Fact]
        public void ParseBulletin_String_IgnoresTheDeclaredEncoding()
        {
            string xml = SampleDocuments.Text("metar-NIL-collect.xml").Replace(Utf8Declaration, Declaration("UTF-16"));

            Result<MeteorologicalBulletin> result = IwxxmParser.ParseBulletin(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
        }

        [Fact]
        public void SigwxParse_String_IgnoresTheDeclaredEncoding()
        {
            string xml = File.ReadAllText(Path.Combine("Samples", "sigwx-swh-2025-2.xml")).Replace(Utf8Declaration, Declaration("UTF-16"));

            Result<Sigwx> result = IwxxmSigwxParser.Parse(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
        }

        [Fact]
        public void Parse_String_WithLeadingByteOrderMarkCharacter_Succeeds()
        {
            Result<IwxxmDocument> result = IwxxmParser.Parse("\uFEFF" + Metar("UTF-16"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
        }

        [Fact]
        public void Parse_String_LargerThanTheByteLimitOnceEncoded_IsASizeLimitError()
        {
            // Fewer characters than the limit, but more UTF-8 bytes (each 'Ü' takes two bytes).
            string xml = Metar("UTF-8").Replace("DONLON/INTERNATIONAL", new string('Ü', 3000));
            var parser = new IwxxmParserBuilder().WithOptions(new IwxxmParserOptions(maxDocumentBytes: xml.Length + 100)).Build();

            Result<IwxxmDocument> result = parser.Process(xml);

            Assert.True(result.IsFailure);
            Assert.Equal("XML-SIZE-LIMIT", Assert.Single(result.Errors).Code);
        }

        [Fact]
        public void Parse_String_WithMoreCharactersThanTheByteLimit_IsASizeLimitError()
        {
            var parser = new IwxxmParserBuilder().WithOptions(new IwxxmParserOptions(maxDocumentBytes: 64)).Build();

            Result<IwxxmDocument> result = parser.Process(Metar("UTF-8"));

            Assert.Equal("XML-SIZE-LIMIT", Assert.Single(result.Errors).Code);
        }

        [Theory]
        [InlineData("<?xml version=\"1.0\" encoding=\"UTF-16\"?><a/>", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><a/>")]
        [InlineData("<?xml version='1.0' encoding='ISO-8859-1' standalone='yes'?><a/>", "<?xml version='1.0' encoding='UTF-8' standalone='yes'?><a/>")]
        [InlineData("<?xml version=\"1.0\"?><a encoding=\"x\"/>", "<?xml version=\"1.0\"?><a encoding=\"x\"/>")]
        [InlineData("<a/>", "<a/>")]
        [InlineData("\uFEFF<a/>", "<a/>")]
        [InlineData("<?xml-stylesheet href=\"a\"?><a/>", "<?xml-stylesheet href=\"a\"?><a/>")]
        public void DeclareUtf8_RewritesOnlyTheEncodingOfTheXmlDeclaration(string input, string expected)
        {
            Assert.Equal(expected, BoundedDocumentBuffer.DeclareUtf8(input));
        }

        private static string Declaration(string encoding) => "<?xml version=\"1.0\" encoding=\"" + encoding + "\"?>";

        private static string Metar(string declaredEncoding) =>
            SampleDocuments.Text("metar-A3-1.xml").Replace(Utf8Declaration, Declaration(declaredEncoding));
    }
}
