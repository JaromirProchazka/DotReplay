using Mono.Cecil;

namespace DotReplay.Experiment;

/// <summary>One captured argument or field, as in run.json <c>captured</c>. Its captureId is its position.</summary>
public sealed record CapturedIdentifier(string Kind, string Name, string Type);

/// <summary>The Target method and the fields captured with it, found in the Target application's assembly.</summary>
public sealed record Target(MethodDefinition Method, IReadOnlyList<FieldDefinition> Fields)
{
    public static Target Find(ModuleDefinition module, MethodSpec spec, IReadOnlyList<string> fieldNames)
    {
        var type = module.GetType(spec.TypeName)
            ?? throw new ExperimentError($"Type {spec.TypeName} is not in {module.Name}.");
        var method = type.Methods.SingleOrDefault(spec.Matches)
            ?? throw new ExperimentError($"Method {spec.Text} is not in {module.Name}.");
        if (!method.HasBody)
        {
            throw new ExperimentError($"Method {spec.Text} has no body to instrument.");
        }

        var fields = fieldNames.Select(name => FindField(module, method, name)).ToList();
        return new Target(method, fields);
    }

    /// <summary>The arguments in declaration order, then the fields in the given order.</summary>
    public IReadOnlyList<CapturedIdentifier> Captured()
    {
        var arguments = Method.Parameters.Select(parameter => new CapturedIdentifier(
            "argument",
            string.IsNullOrEmpty(parameter.Name) ? $"arg{parameter.Index}" : parameter.Name,
            TypeNames.Of(parameter.ParameterType)));
        var duplicateNames = Fields.GroupBy(field => field.Name).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        var fields = Fields.Select(field => new CapturedIdentifier(
            "field",
            duplicateNames.Contains(field.Name) ? $"{field.DeclaringType.Name}.{field.Name}" : field.Name,
            TypeNames.Of(field.FieldType)));
        return [.. arguments, .. fields];
    }

    // A field is given as Namespace.Type.field. An instance field is read from `this`, so it must belong
    // to the Target method's own class.
    static FieldDefinition FindField(ModuleDefinition module, MethodDefinition method, string name)
    {
        var dot = name.LastIndexOf('.');
        var type = dot < 0 ? null : module.GetType(name[..dot]);
        var field = type?.Fields.SingleOrDefault(f => f.Name == name[(dot + 1)..])
            ?? throw new ExperimentError($"Field {name} is not in {module.Name}.");
        if (!field.IsStatic && (method.IsStatic || field.DeclaringType != method.DeclaringType))
        {
            throw new ExperimentError($"Instance field {name} can only be captured on instance methods of {field.DeclaringType.FullName}.");
        }

        return field;
    }
}
