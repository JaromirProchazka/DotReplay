using System.Text.RegularExpressions;
using Mono.Cecil;

namespace DotReplay.Experiment;

/// <summary>A Target method as given on the command line, e.g. <c>Com.Example.Calculator.Add(int,int)</c>.</summary>
public sealed record MethodSpec(string Text, string TypeName, string MethodName, IReadOnlyList<string> ParameterTypes)
{
    public static MethodSpec Parse(string text)
    {
        var compact = Regex.Replace(text, @"\s", "");
        var match = Regex.Match(compact, @"^(?<type>[^(]+)\.(?<method>[^.(]+)\((?<parameters>.*)\)$");
        if (!match.Success)
        {
            throw new ExperimentError($"'{text}' is not a method like Com.Example.Calculator.Add(int,int).");
        }

        var parameters = match.Groups["parameters"].Value;
        return new MethodSpec(
            text,
            match.Groups["type"].Value,
            match.Groups["method"].Value,
            parameters.Length == 0 ? [] : TypeNames.SplitTopLevel(parameters).Select(TypeNames.WithoutAliases).ToList());
    }

    public bool Matches(MethodDefinition method) =>
        method.Name == MethodName
        && method.Parameters.Select(parameter => TypeNames.Of(parameter.ParameterType)).SequenceEqual(ParameterTypes);
}
