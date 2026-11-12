namespace DotReplay.Mock;

/// <summary>
/// What the real System would learn from demo/mock_target_app/args: CalcApp.Main calls
/// Calculator.Add(1, 2), Add(3, 4) and Add(5, 6), and every call returns.
/// </summary>
static class CalculatorTarget
{
    public const string AssemblyFileName = "calc-app.dll";
    public const string SourceFileName = "Calculator.cs";
    public const string Int32Type = "System.Int32";
    public const string TestClass = "Com.Example.CalculatorTest";
    public const string TestMethodPrefix = "TestAdd_Scenario";

    static readonly string[] AddSignatures =
        ["Com.Example.Calculator.Add(int,int)", "Com.Example.Calculator.Add(System.Int32,System.Int32)"];

    // Methods that exist in calc-app.dll but that the mock does not simulate.
    static readonly string[] OtherSignatures =
        ["Com.Example.CalcApp.Main(string[])", "Com.Example.CalcApp.Main(System.String[])"];

    static readonly string[] Int32Names = ["int", Int32Type];

    /// <summary>Parameter names of Add. The captureId of an argument is its position.</summary>
    public static readonly IReadOnlyList<string> ArgumentNames = ["a", "b"];

    /// <summary>The argument values of each call to Add, in the order CalcApp.Main makes them.</summary>
    public static readonly IReadOnlyList<IReadOnlyList<int>> RecordedCalls = [[1, 2], [3, 4], [5, 6]];

    public static bool IsAdd(string signature) => AddSignatures.Contains(WithoutWhitespace(signature));

    public static bool IsOtherMethod(string signature) => OtherSignatures.Contains(WithoutWhitespace(signature));

    public static bool HasParameter(int slot, string type) =>
        slot >= 0 && slot < ArgumentNames.Count && Int32Names.Contains(type);

    static string WithoutWhitespace(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
}
