using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace DotReplay.Experiment;

/// <summary>
/// Reads the findings out of a SharpDetect 2.1.4 HTML report, the only output SharpDetect writes. This is the
/// only class that knows SharpDetect's output: its markup (CSS classes) and the wording of its reasons.
/// </summary>
public static class SharpDetectReport
{
    public static IReadOnlyList<Finding> Parse(string html)
    {
        var container = new HtmlParser().ParseDocument(html).GetElementById("violationsContainer");
        if (container is null)
        {
            return [];
        }

        // FastTrack groups the races by field; the Deadlock plugin lists one card per deadlock.
        var raceGroups = container.Children.Where(element => element.ClassList.Contains("violation-group")).ToList();
        return raceGroups.Count > 0
            ? raceGroups.SelectMany(DataRaces).ToList()
            : container.Children.Where(element => element.ClassList.Contains("violation-card")).Select(Deadlock).ToList();
    }

    static IEnumerable<Finding> DataRaces(IElement group)
    {
        var field = Text(group, ".metadata-fullname");
        return group.QuerySelectorAll("details.violation-card").Select(card => new DataRaceFinding(
            field,
            Panels(card).Select(panel => new RaceAccess(panel.Thread, panel.Frames, AccessType(panel.Reason))).ToList()));
    }

    static Finding Deadlock(IElement card) => new DeadlockFinding(Panels(card).Select(panel =>
    {
        // "Blocked - waiting for lock object [35] owned by thread T2 (Adder)"
        var match = Regex.Match(panel.Reason, @"waiting for lock object \[(?<lock>\d+)\] owned by thread (?<owner>.+)$");
        return match.Success
            ? new DeadlockWait(panel.Thread, panel.Frames, long.Parse(match.Groups["lock"].Value, CultureInfo.InvariantCulture), ThreadName(match.Groups["owner"].Value))
            : throw new ExperimentError($"Unrecognized deadlock reason: {panel.Reason}");
    }).ToList());

    static IEnumerable<(string Thread, string Reason, IReadOnlyList<string> Frames)> Panels(IElement card) =>
        card.QuerySelectorAll(".thread-panel").Select(panel => (
            ThreadName(Text(panel, ".thread-name")),
            Text(panel, ".thread-panel-reason"),
            (IReadOnlyList<string>)panel.QuerySelectorAll(".frame-method")
                .Select(frame => Regex.Replace(frame.TextContent.Trim(), "^at ", ""))
                .ToList()));

    // "Write unordered after previous Write by T3 (Accumulator 2)" → Write
    static string AccessType(string reason) =>
        Regex.Match(reason, "^(?<access>AtomicRead|AtomicWrite|Read|Write) ") is { Success: true } match
            ? match.Groups["access"].Value
            : throw new ExperimentError($"Unrecognized data race reason: {reason}");

    // SharpDetect shows a thread as "T2 (Adder)": its own thread number, then the thread's name.
    static string ThreadName(string text) =>
        Regex.Match(text.Trim(), @"^T\d+ \((?<name>.*)\)$") is { Success: true } match ? match.Groups["name"].Value : text.Trim();

    static string Text(IElement element, string selector) =>
        element.QuerySelector(selector)?.TextContent.Trim() ?? throw new ExperimentError($"The report has no {selector} element.");
}
