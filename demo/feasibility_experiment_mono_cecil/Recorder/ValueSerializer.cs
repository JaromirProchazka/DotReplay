using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DotReplay.Recorder;

/// <summary>
/// Turns .NET values into contract v1 values (value.schema.json). It reads fields only and never calls
/// Target code, apart from enumerating framework collections.
/// </summary>
public static class ValueSerializer
{
    const int MaxDepth = 8;
    const int MaxElements = 100;
    const long MaxSafeInteger = 9_007_199_254_740_991;

    /// <summary>
    /// One snapshot: the values in capture order. A top-level <c>null</c> has no type here, because only
    /// the declared type is known; the adapter adds it.
    /// </summary>
    public static JsonArray Snapshot(IReadOnlyList<object?> values)
    {
        var walk = new Walk();
        return new JsonArray(values
            .Select(value => value is null ? new JsonObject { ["kind"] = "null" } : walk.Value(value, value.GetType(), 0))
            .ToArray<JsonNode?>());
    }

    /// <summary>The contract's type name, e.g. <c>System.Collections.Generic.List&lt;System.Int32&gt;</c>.</summary>
    public static string TypeName(Type type)
    {
        if (type.IsArray)
        {
            return TypeName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        var name = type.IsNested
            ? TypeName(type.DeclaringType!) + "." + type.Name
            : type.Namespace is null ? type.Name : type.Namespace + "." + type.Name;
        var plain = Regex.Replace(name, "`\\d+", "");
        return type.IsGenericType
            ? $"{plain}<{string.Join(",", type.GetGenericArguments().Select(TypeName))}>"
            : plain;
    }

    /// <summary>Walks the values of one snapshot. Object ids are scoped to one snapshot.</summary>
    sealed class Walk
    {
        readonly Dictionary<object, int> ids = new(ReferenceEqualityComparer.Instance);
        readonly HashSet<object> enclosing = new(ReferenceEqualityComparer.Instance);

        public JsonObject Value(object? value, Type declared, int depth)
        {
            if (value is null)
            {
                return Node("null", declared);
            }

            var type = value.GetType();
            if (Primitive(value) is { } primitive)
            {
                return With(Node("primitive", type), "value", primitive);
            }

            if (value is string text)
            {
                return With(Node("string", type), "value", text);
            }

            if (IsSkipped(type))
            {
                return Node("skipped", type);
            }

            if (depth >= MaxDepth)
            {
                return Node("truncated", type);
            }

            if (!type.IsValueType && ids.TryGetValue(value, out var id))
            {
                return With(Node(enclosing.Contains(value) ? "cycle" : "ref", type), "id", id);
            }

            return IsFrameworkCollection(value, type)
                ? Elements((IEnumerable)value, type, depth)
                : Fields(value, type, depth);
        }

        JsonObject Fields(object value, Type type, int depth)
        {
            var node = Tracked(value, Node("object", type));
            var fields = new JsonObject();
            Enter(value);
            foreach (var field in InstanceFields(type))
            {
                fields[FieldName(field)] = Value(field.GetValue(value), field.FieldType, depth + 1);
            }

            Leave(value);
            return With(node, "fields", fields);
        }

        JsonObject Elements(IEnumerable collection, Type type, int depth)
        {
            var node = Tracked(collection, Node("array", type));
            var elementType = ElementType(type);
            var elements = new JsonArray();
            var length = 0;
            Enter(collection);
            foreach (var element in collection)
            {
                if (length++ < MaxElements)
                {
                    elements.Add(Value(element, elementType, depth + 1));
                }
            }

            Leave(collection);
            With(node, "elements", elements);
            return length > MaxElements ? With(node, "length", length) : node;
        }

        // Only reference types get an id: a boxed struct is a new object every time.
        JsonObject Tracked(object value, JsonObject node) =>
            value.GetType().IsValueType ? node : With(node, "id", ids[value] = ids.Count);

        void Enter(object value) => enclosing.Add(value);

        void Leave(object value) => enclosing.Remove(value);
    }

    static JsonObject Node(string kind, Type type) => new() { ["kind"] = kind, ["type"] = TypeName(type) };

    static JsonObject With(JsonObject node, string key, JsonNode? value)
    {
        node[key] = value;
        return node;
    }

    static JsonNode? Primitive(object value) => value switch
    {
        bool b => JsonValue.Create(b),
        char c => JsonValue.Create(c.ToString()),
        sbyte or byte or short or ushort or int or uint => JsonValue.Create(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
        long l => l is >= -MaxSafeInteger and <= MaxSafeInteger ? JsonValue.Create(l) : JsonValue.Create(l.ToString(CultureInfo.InvariantCulture)),
        ulong u => u <= MaxSafeInteger ? JsonValue.Create(u) : JsonValue.Create(u.ToString(CultureInfo.InvariantCulture)),
        float f => float.IsFinite(f) ? JsonValue.Create(f) : JsonValue.Create(f.ToString(CultureInfo.InvariantCulture)),
        double d => double.IsFinite(d) ? JsonValue.Create(d) : JsonValue.Create(d.ToString(CultureInfo.InvariantCulture)),
        decimal m => JsonValue.Create(m),
        _ => null,
    };

    static bool IsSkipped(Type type) =>
        typeof(Delegate).IsAssignableFrom(type)
        || typeof(MemberInfo).IsAssignableFrom(type)
        || type == typeof(IntPtr)
        || type == typeof(UIntPtr);

    // Framework collections are recorded element by element; their internal fields (e.g. List's _items and
    // _size) would be meaningless to the User. Collections of the Target application are recorded by fields,
    // so that no Target code runs.
    static bool IsFrameworkCollection(object value, Type type) =>
        value is Array
        || (type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true
            && (value is ICollection || type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))));

    static Type ElementType(Type type) =>
        type.IsArray
            ? type.GetElementType()!
            : type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0]
              ?? typeof(object);

    // Base class fields first, then the fields of the class itself.
    static IEnumerable<FieldInfo> InstanceFields(Type type) =>
        (type.BaseType is { } baseType ? InstanceFields(baseType) : [])
        .Concat(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));

    // An auto-property's backing field <Name>k__BackingField is shown as the property name.
    static string FieldName(FieldInfo field) =>
        Regex.Match(field.Name, "^<(.+)>k__BackingField$") is { Success: true } match ? match.Groups[1].Value : field.Name;
}
