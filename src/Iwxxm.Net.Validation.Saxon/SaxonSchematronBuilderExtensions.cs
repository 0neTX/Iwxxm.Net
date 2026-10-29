using System;
using Iwxxm.Net.Validation.Saxon;

namespace Iwxxm.Net
{
    /// <summary>
    /// Registration of the official WMO IWXXM 2025-2 Schematron (SaxonCS-HE) in <see cref="IwxxmParserBuilder"/>.
    /// </summary>
    public static class SaxonSchematronBuilderExtensions
    {
        /// <summary>
        /// Replaces the built-in XSLT 1.0 rule subset with the complete official <c>iwxxm.sch</c>
        /// (<see cref="OfficialSchematronValidator"/>) for every parser built by <paramref name="builder"/>.
        /// The validator receives the builder's final <see cref="IwxxmParserOptions"/>.
        /// </summary>
        /// <example>
        /// <code>
        /// IwxxmParser parser = new IwxxmParserBuilder().UseOfficialSchematron().Build();
        /// </code>
        /// </example>
        public static IwxxmParserBuilder UseOfficialSchematron(this IwxxmParserBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            return builder.UseSchematronValidator(options => new OfficialSchematronValidator(options));
        }
    }
}
