using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace DotReplay.Experiment;

/// <summary>
/// The Mono.Cecil experiment's weaver, for several Target methods in one run: <c>ThreadCallRecorder.Enter</c>
/// also receives the id of the Target method, so the recorder knows which method each call belongs to.
/// </summary>
public static class ThreadWeaver
{
    const string RecorderType = "DotReplay.ThreadRecorder.ThreadCallRecorder";

    /// <summary>Changes the Target method in its module; the caller writes the module.</summary>
    public static void Weave(Target target, int targetId, ModuleDefinition recorder)
    {
        var method = target.Method;
        var module = method.Module;
        var recorderType = recorder.GetType(RecorderType);
        var enter = module.ImportReference(recorderType.Methods.Single(m => m.Name == "Enter"));
        var exit = module.ImportReference(recorderType.Methods.Single(m => m.Name == "Exit"));

        var body = method.Body;
        body.SimplifyMacros();
        var callIndex = new VariableDefinition(module.TypeSystem.Int32);
        body.Variables.Add(callIndex);
        var il = body.GetILProcessor();

        var returns = body.Instructions.Where(instruction => instruction.OpCode == OpCodes.Ret).ToList();
        var first = body.Instructions[0];
        foreach (var instruction in (Instruction[])[il.Create(OpCodes.Ldc_I4, targetId), .. Values(target, il), il.Create(OpCodes.Call, enter), il.Create(OpCodes.Stloc, callIndex)])
        {
            il.InsertBefore(first, instruction);
        }

        foreach (var ret in returns)
        {
            // The old ret becomes a nop, so branches that targeted it also run the exit recording.
            ret.OpCode = OpCodes.Nop;
            ret.Operand = null;
            var previous = ret;
            foreach (var instruction in (Instruction[])[il.Create(OpCodes.Ldloc, callIndex), .. Values(target, il), il.Create(OpCodes.Call, exit), il.Create(OpCodes.Ret)])
            {
                il.InsertAfter(previous, instruction);
                previous = instruction;
            }
        }

        body.OptimizeMacros();
    }

    /// <summary>Instructions that push a new <c>object[]</c> with the arguments, then the fields.</summary>
    static IEnumerable<Instruction> Values(Target target, ILProcessor il)
    {
        var loads = target.Method.Parameters
            .Select(parameter => (Load: new[] { il.Create(OpCodes.Ldarg, parameter) }, Type: parameter.ParameterType))
            .Concat(target.Fields.Select(field => (
                Load: field.IsStatic
                    ? new[] { il.Create(OpCodes.Ldsfld, field) }
                    : new[] { il.Create(OpCodes.Ldarg_0), il.Create(OpCodes.Ldfld, field) },
                Type: field.FieldType)))
            .ToList();

        yield return il.Create(OpCodes.Ldc_I4, loads.Count);
        yield return il.Create(OpCodes.Newarr, target.Method.Module.TypeSystem.Object);
        for (var i = 0; i < loads.Count; i++)
        {
            yield return il.Create(OpCodes.Dup);
            yield return il.Create(OpCodes.Ldc_I4, i);
            foreach (var load in loads[i].Load)
            {
                yield return load;
            }

            if (loads[i].Type.IsValueType || loads[i].Type.IsGenericParameter)
            {
                yield return il.Create(OpCodes.Box, loads[i].Type);
            }

            yield return il.Create(OpCodes.Stelem_Ref);
        }
    }
}
