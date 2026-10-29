using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Iwxxm.Net.Tests.GoldenMaster
{
    /// <summary>
    /// Test-only, reflection-based canonical serializer of a mapped Domain object graph used by the
    /// characterization (golden-master) tests.
    /// </summary>
    /// <remarks>
    /// The output is deterministic and culture-invariant: public instance properties are written in ordinal
    /// name order, numbers use round-trip invariant formatting, instants use ISO 8601 (<c>O</c>), and every
    /// object is prefixed by its runtime type name. A property getter that throws is recorded as
    /// <c>&lt;throws ExceptionType&gt;</c>. This type is not a production dependency.
    /// </remarks>
    internal static class CanonicalDomainDumper
    {
        private const int MaxDepth = 64;

        public static string Dump(object? value)
        {
            var sb = new StringBuilder();
            var visiting = new HashSet<object>(IdentityComparer.Instance);
            Write(sb, value, 0, visiting);
            sb.Append('\n');
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object? value, int depth, HashSet<object> visiting)
        {
            if (depth > MaxDepth)
            {
                sb.Append("<max-depth>");
                return;
            }

            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (TryWriteScalar(sb, value))
                return;

            Type type = value.GetType();
            bool isReference = !type.IsValueType;
            if (isReference && !visiting.Add(value))
            {
                sb.Append("<cycle ").Append(TypeName(type)).Append('>');
                return;
            }

            try
            {
                if (value is IEnumerable enumerable)
                {
                    WriteSequence(sb, enumerable, depth, visiting);
                    return;
                }

                WriteObject(sb, value, type, depth, visiting);
            }
            finally
            {
                if (isReference)
                    visiting.Remove(value);
            }
        }

        private static bool TryWriteScalar(StringBuilder sb, object value)
        {
            switch (value)
            {
                case string s:
                    sb.Append('"').Append(Escape(s)).Append('"');
                    return true;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return true;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    return true;
                case float f:
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    return true;
                case decimal m:
                    sb.Append(m.ToString(CultureInfo.InvariantCulture));
                    return true;
                case DateTimeOffset dto:
                    sb.Append(dto.ToString("O", CultureInfo.InvariantCulture));
                    return true;
                case DateTime dt:
                    sb.Append(dt.ToString("O", CultureInfo.InvariantCulture));
                    return true;
                case TimeSpan ts:
                    sb.Append(ts.ToString("c", CultureInfo.InvariantCulture));
                    return true;
                case Enum e:
                    sb.Append(e.GetType().Name).Append('.').Append(e.ToString());
                    return true;
                case IFormattable formattable when value.GetType().IsPrimitive:
                    sb.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                    return true;
                case char c:
                    sb.Append('\'').Append(Escape(c.ToString())).Append('\'');
                    return true;
                case Uri uri:
                    sb.Append('"').Append(Escape(uri.OriginalString)).Append('"');
                    return true;
                default:
                    return false;
            }
        }

        private static void WriteSequence(StringBuilder sb, IEnumerable enumerable, int depth, HashSet<object> visiting)
        {
            var items = enumerable.Cast<object?>().ToList();
            if (items.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append('[');
            foreach (object? item in items)
            {
                NewLine(sb, depth + 1);
                Write(sb, item, depth + 1, visiting);
            }

            NewLine(sb, depth);
            sb.Append(']');
        }

        private static void WriteObject(StringBuilder sb, object value, Type type, int depth, HashSet<object> visiting)
        {
            PropertyInfo[] properties = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetMethod != null && p.GetMethod.IsPublic)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToArray();

            sb.Append(TypeName(type)).Append(" {");
            foreach (PropertyInfo property in properties)
            {
                NewLine(sb, depth + 1);
                sb.Append(property.Name).Append(" = ");

                object? propertyValue;
                try
                {
                    propertyValue = property.GetValue(value);
                }
                catch (TargetInvocationException ex)
                {
                    sb.Append("<throws ").Append(ex.InnerException?.GetType().Name ?? ex.GetType().Name).Append('>');
                    continue;
                }

                Write(sb, propertyValue, depth + 1, visiting);
            }

            NewLine(sb, depth);
            sb.Append('}');
        }

        private static string TypeName(Type type)
        {
            if (!type.IsGenericType)
                return type.Name;

            string name = type.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0)
                name = name.Substring(0, tick);

            return name + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">";
        }

        private static void NewLine(StringBuilder sb, int depth)
        {
            sb.Append('\n').Append(' ', depth * 2);
        }

        private static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            public static readonly IdentityComparer Instance = new IdentityComparer();

            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
