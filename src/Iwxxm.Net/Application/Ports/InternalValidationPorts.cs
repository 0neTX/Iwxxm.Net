using System.Collections.Generic;
using System.IO;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;

namespace Iwxxm.Net.Application.Ports
{
    /// <summary>
    /// Outbound port for structural XML validation against embedded XSD schemas.
    /// </summary>
    internal interface IXmlSchemaValidator
    {
        IReadOnlyList<ValidationError> Validate(Stream xmlStream);
    }

    /// <summary>
    /// Outbound port for business rule validation using precompiled Schematron XSLT transformations.
    /// </summary>
    internal interface ISchematronValidator
    {
        IReadOnlyList<ValidationError> Validate(Stream xmlStream);
    }

    /// <summary>
    /// Outbound port for deserializing a validated XML stream into the XSD-generated transport model.
    /// Decouples the Application layer from System.Xml.Serialization and Infrastructure generated types.
    /// </summary>
    /// <typeparam name="TDto">Type of the deserialized XSD transport object.</typeparam>
    internal interface ISigwxXmlDeserializer<TDto>
    {
        Result<TDto> Deserialize(Stream xmlStream);
    }

    /// <summary>
    /// Outbound port for the Anti-Corruption Layer (ACL) mapper that converts the XSD-generated
    /// transport object into the pure <see cref="SignificantWeatherReport"/> Domain Aggregate Root.
    /// </summary>
    /// <typeparam name="TDto">Type of the deserialized XSD transport object.</typeparam>
    internal interface ISigwxAclMapper<in TDto>
    {
        Result<SignificantWeatherReport> MapToDomain(TDto generatedReport);
    }
}
