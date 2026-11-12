namespace Com.Example;

public class CalcApp
{
    public static void Main(string[] args)
    {
        Calculator c = new Calculator();
        // invoke target method multiple times
        Console.WriteLine(c.Add(1, 2));
        Console.WriteLine(c.Add(3, 4));
        Console.WriteLine(c.Add(5, 6));
    }
}
