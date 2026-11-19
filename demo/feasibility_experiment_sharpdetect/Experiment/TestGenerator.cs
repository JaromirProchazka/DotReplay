using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DotReplay.Experiment;

/// <summary>
/// Generates xUnit tests from analysis.json alone, one test per finding. Each test replays the recorded calls of the
/// finding's threads on their own threads, at the same time, and fails while the concurrency error is present:
/// a deadlock fails on a timeout, a data race on a lost update.
/// </summary>
public static class TestGenerator
{
    public const string ProjectFile = "GeneratedTests.csproj";
    public const string TestsFile = "ConcurrencyTests.cs";

    /// <summary>The generated test project, as file name → content.</summary>
    public static IReadOnlyDictionary<string, string> Generate(JsonObject analysis)
    {
        var findings = analysis["findings"]!.AsArray().Select(finding => finding!.AsObject()).ToList();
        if (findings.Count == 0)
        {
            throw new ExperimentError("analysis.json has no findings, so there is nothing to generate.");
        }

        var tests = findings.Select((finding, index) => finding["kind"]!.GetValue<string>() switch
        {
            "deadlock" => DeadlockTest(analysis, finding, index),
            "dataRace" => DataRaceTest(analysis, finding, index),
            var kind => throw new ExperimentError($"Unknown finding kind {kind}."),
        });

        return new Dictionary<string, string>
        {
            [ProjectFile] = Project(analysis["assembly"]!.GetValue<string>()),
            [TestsFile] = TestClass(tests),
        };
    }

    static string DeadlockTest(JsonObject analysis, JsonObject finding, int index)
    {
        var threads = finding["threads"]!.AsArray().Select(thread => thread!.AsObject())
            .Select(thread => (ThreadId: thread["threadId"]!.GetValue<int>(), Calls: Ints(thread["callIndices"]))).ToList();
        var names = threads.Select(thread => ThreadName(analysis, thread.ThreadId)).ToList();
        var receiverType = ReceiverType(analysis, threads.SelectMany(thread => thread.Calls));
        return $$"""
                [Fact]
                public void Deadlock{{index}}_{{Identifier(string.Join("_", names))}}()
                {
                    var receiver = new {{receiverType}}();

                    var finished = Replay(
            {{string.Join(",\n", threads.Select(thread => ReplayedThread(analysis, "receiver", thread.ThreadId, thread.Calls)))}});

                    Assert.True(finished, {{Literal($"Deadlock: {string.Join(" and ", names)} did not finish within the timeout.")}});
                }
            """;
    }

    static string DataRaceTest(JsonObject analysis, JsonObject finding, int index)
    {
        var field = finding["field"]!.GetValue<string>();
        var threads = finding["accesses"]!.AsArray().Select(access => access!.AsObject())
            .GroupBy(access => access["threadId"]!.GetValue<int>())
            .Select(group => (ThreadId: group.Key, Calls: group.SelectMany(access => Ints(access["callIndices"])).Distinct().Order().ToList()))
            .ToList();
        var allCalls = threads.SelectMany(thread => thread.Calls).Order().ToList();
        var receiverType = ReceiverType(analysis, allCalls);
        var fieldName = field[(field.LastIndexOf('.') + 1)..];
        return $$"""
                [Fact]
                public void DataRace{{index}}_{{Identifier(field[(field.LastIndexOf('.', field.LastIndexOf('.') - 1) + 1)..])}}()
                {
                    // The same calls, one after another, give the result without the race.
                    var sequential = new {{receiverType}}();
            {{string.Concat(allCalls.Select(call => $"        {CallExpression(analysis, "sequential", call)};\n"))}}
                    var concurrent = new {{receiverType}}();
                    var finished = Replay(
            {{string.Join(",\n", threads.Select(thread => ReplayedThread(analysis, "concurrent", thread.ThreadId, thread.Calls)))}});

                    Assert.True(finished, "The replayed threads did not finish within the timeout.");
                    Assert.True(
                        Equals(FieldValue(sequential, {{Literal(fieldName)}}), FieldValue(concurrent, {{Literal(fieldName)}})),
                        $"Data race on {{field}}: {FieldValue(concurrent, {{Literal(fieldName)}})} after the concurrent calls, {FieldValue(sequential, {{Literal(fieldName)}})} after the same calls one after another.");
                }
            """;
    }

    static string ReplayedThread(JsonObject analysis, string receiver, int threadId, IReadOnlyList<int> calls) =>
        calls.Count == 0
            ? throw new ExperimentError($"Thread {ThreadName(analysis, threadId)} has no recorded call of a Target method to replay.")
            : $"            ({Literal(ThreadName(analysis, threadId))}, () => {{ {string.Concat(calls.Select(call => CallExpression(analysis, receiver, call) + "; "))}}})";

    static string CallExpression(JsonObject analysis, string receiver, int callIndex)
    {
        var call = analysis["calls"]![callIndex]!.AsObject();
        var target = analysis["targets"]![call["target"]!.GetValue<int>()]!.AsObject();
        var method = MethodSpec.Parse(target["signature"]!.GetValue<string>());
        var arguments = target["captured"]!.AsArray()
            .Where(captured => captured!["kind"]!.GetValue<string>() == "argument")
            .Select(captured => Literal(call["entry"]![captured!["captureId"]!.ToString()]!.AsObject()));
        return $"{receiver}.{method.MethodName}({string.Join(", ", arguments)})";
    }

    // All calls of a test go to one shared receiver, created with its parameterless constructor.
    static string ReceiverType(JsonObject analysis, IEnumerable<int> callIndices)
    {
        var types = callIndices
            .Select(call => analysis["calls"]![call]!["target"]!.GetValue<int>())
            .Select(target => MethodSpec.Parse(analysis["targets"]![target]!["signature"]!.GetValue<string>()).TypeName)
            .Distinct()
            .ToList();
        return types.Count == 1
            ? types[0]
            : throw new ExperimentError($"A finding must involve calls on one receiver type, not {types.Count}.");
    }

    static string ThreadName(JsonObject analysis, int threadId) => analysis["threads"]![threadId]!["name"]!.GetValue<string>();

    static List<int> Ints(JsonNode? array) => array!.AsArray().Select(item => item!.GetValue<int>()).ToList();

    /// <summary>A recorded value as a C# literal. Only primitives, strings and null can be replayed.</summary>
    static string Literal(JsonObject value)
    {
        var type = value["type"]!.GetValue<string>();
        return value["kind"]!.GetValue<string>() switch
        {
            "null" => "null",
            "string" => Literal(value["value"]!.GetValue<string>()),
            "primitive" => type switch
            {
                "System.Boolean" => value["value"]!.GetValue<bool>() ? "true" : "false",
                "System.Char" => $"'{value["value"]!.GetValue<string>()}'",
                "System.Int64" => value["value"]!.ToJsonString() + "L",
                "System.Double" => value["value"]!.ToJsonString() + "d",
                "System.Single" => value["value"]!.ToJsonString() + "f",
                "System.Decimal" => value["value"]!.ToJsonString() + "m",
                _ => value["value"]!.ToJsonString(),
            },
            var kind => throw new ExperimentError($"A {kind} value of type {type} cannot be replayed in a generated test."),
        };
    }

    // A JSON string is also a valid C# string literal.
    static string Literal(string text) => JsonSerializer.Serialize(text);

    static string Identifier(string text) => Regex.Replace(text, @"[^A-Za-z0-9_]", "_");

    static string TestClass(IEnumerable<string> tests) => $$"""
        // <auto-generated>Generated by sharpdetect-experiment generate-tests from analysis.json.</auto-generated>
        using System.Reflection;
        using Xunit;

        namespace DotReplay.Generated;

        public class ConcurrencyTests
        {
            static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        {{string.Join("\n\n", tests)}}

            /// <summary>Runs each thread's calls on its own background thread, all starting together.</summary>
            static bool Replay(params (string Name, Action Calls)[] threads)
            {
                var start = new Barrier(threads.Length);
                var running = threads
                    .Select(thread => new Thread(() => { start.SignalAndWait(); thread.Calls(); }) { Name = thread.Name, IsBackground = true })
                    .ToList();
                running.ForEach(thread => thread.Start());
                return running.All(thread => thread.Join(Timeout));
            }

            static object? FieldValue(object receiver, string field) =>
                receiver.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(receiver);
        }

        """;

    static string Project(string assembly) => $$"""
        <Project Sdk="Microsoft.NET.Sdk">

          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <IsPackable>false</IsPackable>
            <IsTestProject>true</IsTestProject>
            <!-- The Target application under test; override with -p:TargetAssembly=<path> -->
            <TargetAssembly Condition="'$(TargetAssembly)' == ''">{{assembly}}</TargetAssembly>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
            <PackageReference Include="xunit" Version="2.9.2" />
            <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
          </ItemGroup>

          <ItemGroup>
            <Reference Include="$([System.IO.Path]::GetFileNameWithoutExtension('$(TargetAssembly)'))" HintPath="$(TargetAssembly)" />
          </ItemGroup>

        </Project>

        """;
}
