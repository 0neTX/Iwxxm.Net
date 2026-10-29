using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Iwxxm.Net.Infrastructure.Serialization
{
    /// <summary>
    /// Minimal reflection-based reader that populates the internal XSD transport model
    /// (<c>Iwxxm20252.Generated</c>) from an <see cref="XElement"/> tree, honouring the
    /// <see cref="XmlElementAttribute"/>, <see cref="XmlAttributeAttribute"/>, <see cref="XmlTextAttribute"/>,
    /// <see cref="XmlIgnoreAttribute"/> and <see cref="XmlTypeAttribute"/> metadata used by that model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="XmlSerializer"/> only processes public types; the transport model is internal so that it does not
    /// leak into the public API (AUD-H8). This reader reproduces the subset of <see cref="XmlSerializer"/> semantics the
    /// model relies on: unknown elements and attributes are ignored, the last occurrence wins for single-valued
    /// members, repeated elements are appended to <see cref="List{T}"/> members, <c>xsi:nil="true"</c> yields
    /// <c>null</c> for members declared with <see cref="XmlElementAttribute.IsNullable"/>, and the
    /// <c>{Member}Specified</c> companion property is set to <c>true</c> when a value is read.
    /// </para>
    /// <para>Conversion failures are reported as <see cref="InvalidOperationException"/>, like <see cref="XmlSerializer"/>.</para>
    /// </remarks>
    internal static class AttributeDrivenXmlReader
    {
        private static readonly XNamespace XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

        private static readonly ConcurrentDictionary<Type, TypeMap> TypeMaps = new ConcurrentDictionary<Type, TypeMap>();

        /// <summary>Creates and populates an instance of <typeparamref name="T"/> from <paramref name="element"/>.</summary>
        public static T Read<T>(XElement element)
            where T : class
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            return (T)ReadObject(typeof(T), element);
        }

        private static object ReadObject(Type type, XElement element)
        {
            TypeMap map = TypeMaps.GetOrAdd(type, BuildTypeMap);
            object instance = map.Create();

            foreach (XAttribute attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                    continue;

                if (map.Attributes.TryGetValue(attribute.Name, out MemberMap? member))
                {
                    member.SetValue(instance, ConvertScalar(member.ValueType, attribute.Value, member.Name));
                }
            }

            if (map.Text != null)
            {
                string text = string.Concat(element.Nodes().OfType<XText>().Select(t => t.Value));
                map.Text.SetValue(instance, ConvertScalar(map.Text.ValueType, text, map.Text.Name));
            }

            foreach (XElement child in element.Elements())
            {
                if (!map.Elements.TryGetValue(child.Name, out MemberMap? member))
                    continue;

                object? value = ReadElementValue(member, child);
                if (member.IsList)
                {
                    if (value != null)
                    {
                        member.GetOrCreateList(instance).Add(value);
                    }
                }
                else
                {
                    member.SetValue(instance, value);
                }
            }

            return instance;
        }

        private static object? ReadElementValue(MemberMap member, XElement child)
        {
            if (member.IsNullable && IsNil(child))
                return null;

            Type valueType = member.ValueType;
            if (IsScalar(valueType))
                return ConvertScalar(valueType, child.Value, member.Name);

            return ReadObject(valueType, child);
        }

        private static bool IsNil(XElement element)
        {
            string? nil = (string?)element.Attribute(XsiNamespace + "nil");
            return nil != null && (nil.Trim() == "true" || nil.Trim() == "1");
        }

        private static bool IsScalar(Type type) =>
            type == typeof(string) || type == typeof(int) || type == typeof(long) || type == typeof(double) ||
            type == typeof(decimal) || type == typeof(bool) || type == typeof(DateTime);

        private static object? ConvertScalar(Type type, string raw, string memberName)
        {
            try
            {
                if (type == typeof(string))
                    return raw;
                if (type == typeof(int))
                    return XmlConvert.ToInt32(raw.Trim());
                if (type == typeof(long))
                    return XmlConvert.ToInt64(raw.Trim());
                if (type == typeof(double))
                    return XmlConvert.ToDouble(raw.Trim());
                if (type == typeof(decimal))
                    return XmlConvert.ToDecimal(raw.Trim());
                if (type == typeof(bool))
                    return XmlConvert.ToBoolean(raw.Trim());
                if (type == typeof(DateTime))
                    return XmlConvert.ToDateTime(raw.Trim(), XmlDateTimeSerializationMode.RoundtripKind);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture, "The value '{0}' of member '{1}' is not a valid {2}.", raw, memberName, type.Name),
                    ex);
            }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture, "The value '{0}' of member '{1}' is out of range for {2}.", raw, memberName, type.Name),
                    ex);
            }

            throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, "Member '{0}' has the unsupported scalar type '{1}'.", memberName, type.FullName));
        }

        private static TypeMap BuildTypeMap(Type type)
        {
            if (type.IsAbstract)
                throw new InvalidOperationException($"Cannot instantiate abstract transport type '{type.FullName}'.");

            ConstructorInfo? constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (constructor == null)
                throw new InvalidOperationException($"Transport type '{type.FullName}' has no parameterless constructor.");

            var elements = new Dictionary<XName, MemberMap>();
            var attributes = new Dictionary<XName, MemberMap>();
            MemberMap? text = null;

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanWrite || property.IsDefined(typeof(XmlIgnoreAttribute), inherit: true))
                    continue;

                PropertyInfo? specified = type.GetProperty(property.Name + "Specified", BindingFlags.Instance | BindingFlags.Public);
                if (specified != null && (specified.PropertyType != typeof(bool) || !specified.CanWrite))
                    specified = null;

                string defaultNamespace = GetTypeNamespace(property.DeclaringType ?? type);

                foreach (XmlElementAttribute elementAttribute in property.GetCustomAttributes<XmlElementAttribute>(inherit: true))
                {
                    XName name = XName.Get(elementAttribute.ElementName, elementAttribute.Namespace ?? defaultNamespace);
                    elements[name] = new MemberMap(property, specified, elementAttribute.IsNullable);
                }

                XmlAttributeAttribute? attributeAttribute = property.GetCustomAttribute<XmlAttributeAttribute>(inherit: true);
                if (attributeAttribute != null)
                {
                    XName name = XName.Get(attributeAttribute.AttributeName, attributeAttribute.Namespace ?? string.Empty);
                    attributes[name] = new MemberMap(property, specified, isNullable: false);
                }

                if (property.IsDefined(typeof(XmlTextAttribute), inherit: true))
                {
                    text = new MemberMap(property, specified, isNullable: false);
                }
            }

            return new TypeMap(() => constructor.Invoke(null), elements, attributes, text);
        }

        private static string GetTypeNamespace(Type type) =>
            type.GetCustomAttribute<XmlTypeAttribute>(inherit: false)?.Namespace ?? string.Empty;

        private sealed class TypeMap
        {
            public TypeMap(Func<object> create, Dictionary<XName, MemberMap> elements, Dictionary<XName, MemberMap> attributes, MemberMap? text)
            {
                Create = create;
                Elements = elements;
                Attributes = attributes;
                Text = text;
            }

            public Func<object> Create { get; }
            public Dictionary<XName, MemberMap> Elements { get; }
            public Dictionary<XName, MemberMap> Attributes { get; }
            public MemberMap? Text { get; }
        }

        private sealed class MemberMap
        {
            private readonly PropertyInfo _property;
            private readonly PropertyInfo? _specified;

            public MemberMap(PropertyInfo property, PropertyInfo? specified, bool isNullable)
            {
                _property = property;
                _specified = specified;
                IsNullable = isNullable;

                Type propertyType = property.PropertyType;
                if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    IsList = true;
                    ValueType = propertyType.GetGenericArguments()[0];
                }
                else
                {
                    ValueType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
                }
            }

            public string Name => _property.DeclaringType?.Name + "." + _property.Name;
            public bool IsNullable { get; }
            public bool IsList { get; }
            public Type ValueType { get; }

            public void SetValue(object instance, object? value)
            {
                _property.SetValue(instance, value);
                if (value != null)
                {
                    _specified?.SetValue(instance, true);
                }
            }

            public IList GetOrCreateList(object instance)
            {
                var list = (IList?)_property.GetValue(instance);
                if (list == null)
                {
                    list = (IList)Activator.CreateInstance(_property.PropertyType)!;
                    _property.SetValue(instance, list);
                }

                return list;
            }
        }
    }
}
