using System.Text.RegularExpressions;
using Mono.Cecil;

namespace DotReplay.Experiment;

/// <summary>Type names in the contract's form (<c>System.Int32</c>) and their short display names (<c>int</c>).</summary>
public static class TypeNames
{
    static readonly Dictionary<string, string> Aliases = new()
    {
        ["System.Boolean"] = "bool",
        ["System.Byte"] = "byte",
        ["System.SByte"] = "sbyte",
        ["System.Char"] = "char",
        ["System.Int16"] = "short",
        ["System.UInt16"] = "ushort",
        ["System.Int32"] = "int",
        ["System.UInt32"] = "uint",
        ["System.Int64"] = "long",
        ["System.UInt64"] = "ulong",
        ["System.Single"] = "float",
        ["System.Double"] = "double",
        ["System.Decimal"] = "decimal",
        ["System.String"] = "string",
        ["System.Object"] = "object",
    };

    /// <summary>Cecil's full name (<c>List`1&lt;System.Int32&gt;</c>, <c>Outer/Inner</c>) in the contract's form.</summary>
    public static string Of(TypeReference type) => Regex.Replace(type.FullName.Replace('/', '.'), "`\\d+", "");

    /// <summary>Replaces every C# alias in a type name with the full name: <c>List&lt;int&gt;</c> → <c>List&lt;System.Int32&gt;</c>.</summary>
    public static string WithoutAliases(string name) =>
        Regex.Replace(name, @"[A-Za-z_][\w.]*", match => Aliases.FirstOrDefault(alias => alias.Value == match.Value).Key ?? match.Value);

    /// <summary>The short display name for run.json <c>typeNames</c>: <c>System.Collections.Generic.List&lt;System.Int32&gt;</c> → <c>List&lt;int&gt;</c>.</summary>
    public static string Short(string name)
    {
        if (Aliases.TryGetValue(name, out var alias))
        {
            return alias;
        }

        if (name.EndsWith(']'))
        {
            var bracket = name.LastIndexOf('[');
            return Short(name[..bracket]) + name[bracket..];
        }

        var open = name.IndexOf('<');
        return open < 0
            ? Simple(name)
            : $"{Simple(name[..open])}<{string.Join(",", SplitTopLevel(name[(open + 1)..^1]).Select(Short))}>";
    }

    /// <summary>Splits a comma separated list of type names, ignoring commas inside generic arguments.</summary>
    public static IEnumerable<string> SplitTopLevel(string list)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < list.Length; i++)
        {
            switch (list[i])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    yield return list[start..i];
                    start = i + 1;
                    break;
            }
        }

        yield return list[start..];
    }

    static string Simple(string name) => name[(name.LastIndexOf('.') + 1)..];
}
