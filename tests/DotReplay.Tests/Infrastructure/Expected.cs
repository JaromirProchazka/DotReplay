using System.Text.Json.Nodes;
using Xunit;

namespace DotReplay.Tests.Infrastructure;

/// <summary>Expected outputs (golden files) in Expected/&lt;scenario&gt;/.</summary>
public static class Expected
{
    public const string Calculator = "calculator";

    // Values that differ on every run and are left out of the comparison.
    static readonly string[] VolatileKeys = ["createdAt", "completedAt"];

    public static string PathOf(string scenario, string file) =>
        Path.Combine(AppContext.BaseDirectory, "Expected", scenario, file);

    public static JsonObject Read(string scenario, string file) =>
        (JsonObject)JsonNode.Parse(File.ReadAllText(PathOf(scenario, file)))!;

    /// <summary>Asserts that a Session file equals the expected file, apart from timestamps.</summary>
    public static void Matches(SessionDirectory session, string scenario, string file) =>
        Matches(Read(scenario, file), session.Read(file), $"{file} (Expected/{scenario}/{file})");

    /// <summary>Asserts that two JSON documents are equal, apart from timestamps.</summary>
    public static void Matches(JsonNode expected, JsonNode actual, string label)
    {
        var expectedContent = WithoutVolatileKeys(expected);
        var actualContent = WithoutVolatileKeys(actual);
        Assert.True(JsonNode.DeepEquals(expectedContent, actualContent),
            $"{label} differs from the expected content.\nExpected:\n{expectedContent}\nActual:\n{actualContent}");
    }

    static JsonNode WithoutVolatileKeys(JsonNode node)
    {
        var copy = node.DeepClone();
        Strip(copy);
        return copy;
    }

    static void Strip(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in VolatileKeys)
                {
                    obj.Remove(key);
                }

                foreach (var (_, value) in obj)
                {
                    Strip(value);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Strip(item);
                }

                break;
        }
    }
}
