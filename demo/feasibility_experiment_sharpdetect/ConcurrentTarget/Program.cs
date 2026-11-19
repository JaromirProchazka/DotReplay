namespace Com.Example.Concurrent;

/// <summary>Runs one scenario: <c>deadlock</c> or <c>race</c>.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var calculator = new SharedCalculator();
        Thread[] threads = args.FirstOrDefault() switch
        {
            "deadlock" =>
            [
                Named("Adder", () => Console.WriteLine(calculator.Add(1, 2))),
                Named("Subtractor", () => Console.WriteLine(calculator.Subtract(5, 3))),
            ],
            "race" =>
            [
                Named("Accumulator 1", () => { calculator.Accumulate(1); calculator.Accumulate(2); calculator.Accumulate(3); }),
                Named("Accumulator 2", () => { calculator.Accumulate(10); calculator.Accumulate(20); calculator.Accumulate(30); }),
            ],
            _ => [],
        };
        if (threads.Length == 0)
        {
            Console.Error.WriteLine("Usage: concurrent-target deadlock|race");
            return 2;
        }

        foreach (var thread in threads)
        {
            thread.Start();
        }

        // Deadlocked threads are background threads, so the process still ends after the timeout.
        var finished = threads.All(thread => thread.Join(TimeSpan.FromSeconds(2)));
        Console.WriteLine(finished ? "All threads finished." : "Threads did not finish: deadlock.");
        return 0;
    }

    static Thread Named(string name, Action work) => new(() => work()) { Name = name, IsBackground = true };
}
