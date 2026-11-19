namespace Com.Example.Concurrent;

/// <summary>
/// The Calculator shared by several threads, with an obvious deadlock (Add and Subtract take the two locks in
/// opposite order) and an obvious data race (Accumulate updates total without a lock). The sleeps inside the
/// critical sections make both bugs happen on every run. The FIXED build removes both bugs.
/// </summary>
public class SharedCalculator
{
    readonly object inputLock = new();
    readonly object resultLock = new();
    int total;

    public int Add(int a, int b)
    {
        lock (inputLock)
        {
            Thread.Sleep(100);
            lock (resultLock)
            {
                return a + b;
            }
        }
    }

    public int Subtract(int a, int b)
    {
#if FIXED
        lock (inputLock)
        {
            Thread.Sleep(100);
            lock (resultLock)
            {
                return a - b;
            }
        }
#else
        lock (resultLock)
        {
            Thread.Sleep(100);
            lock (inputLock)
            {
                return a - b;
            }
        }
#endif
    }

    public int Accumulate(int value)
    {
#if FIXED
        lock (resultLock)
        {
            var current = total;
            Thread.Sleep(10);
            total = current + value;
            return total;
        }
#else
        var current = total;
        Thread.Sleep(10);
        total = current + value;
        return total;
#endif
    }
}
