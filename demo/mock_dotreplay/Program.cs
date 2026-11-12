namespace DotReplay.Mock;

static class Program
{
    const string Usage = """
        Usage:
          dotreplay [options]                  Full run: analysis, test generation and test running
          dotreplay analyze [options]          Analysis only
          dotreplay generate-tests --run-dir <dir> [--max-argument-combinations <count>]
          dotreplay run-tests --run-dir <dir> [--test <testId>]...

        Options of the full run and analyze:
              --assembly <path>            Target application assembly (calc-app.dll)
          -s, --source <path>              Target application source code
          -m, --method <signature>         Target method, e.g. Com.Example.Calculator.Add(int,int)
          -p, --parameters <slot:type,...> Arguments to capture, e.g. 0:int,1:int
          -f, --fields <type:name,...>     Fields to capture
          -o, --output-dir <dir>           Session directory for the results
          -a, --args <args>                Target application arguments (separated by ' ')
          -l, --language <name>            Language (default: dotnet)
          -t, --test-strategy <name>       Test generation strategy (default: trace-based-basic)
          -r, --trace-mode <mode>          Trace mode (default: naive)
              --strategy <name>            Analysis Strategy (default: record)
              --static-method              The Target method is static
              --void-method                The Target method returns void
              --config <file>              JSON Run configuration; command-line options override it
              --max-argument-combinations <count>
                                           Full run only: limit on generated tests (default: no limit)
          -h, --help                       Show this help

        This is a mock: it only simulates the Calculator in demo/mock_target_app/args.
        """;

    static int Main(string[] args)
    {
        if (args.Any(arg => arg is "-h" or "--help"))
        {
            Console.WriteLine(Usage);
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        var rest = args.Skip(1).ToArray();
        try
        {
            var succeeded = args.FirstOrDefault() switch
            {
                "analyze" => Controller.Analyze(ArgumentParser.ParseAnalysis(rest, fullRun: false), now),
                "generate-tests" => Controller.GenerateTests(ArgumentParser.ParseGenerateTests(rest), now),
                "run-tests" => Controller.RunTests(ArgumentParser.ParseRunTests(rest), now),
                _ => Controller.FullRun(ArgumentParser.ParseAnalysis(args, fullRun: true), now),
            };
            return succeeded ? 0 : 1;
        }
        catch (StageError error)
        {
            Controller.Report(error);
            Console.Error.WriteLine("Run 'dotreplay --help' for usage.");
            return 1;
        }
    }
}
