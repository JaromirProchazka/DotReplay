namespace Com.Example.Extra;

/// <summary>Calls every Target method of the extra cases a few times (see README.md, Scope).</summary>
public static class Program
{
    public static void Main()
    {
        // Complex objects: a null field, a cycle and a shared reference.
        var graph = new Graph();
        Console.WriteLine(graph.Describe(new Node("single")));
        var first = new Node("first");
        var second = new Node("second") { Next = first };
        first.Next = second;
        Console.WriteLine(graph.Describe(first));
        var shared = new Node("shared");
        Console.WriteLine(graph.Describe(new Node("owner") { Next = shared, Partner = shared }));

        // Collections.
        var shop = new Shop();
        Console.WriteLine(shop.Total(new Order(1)
        {
            Items = [new Item("pen", 1.5m, 2), new Item("book", 12m, 1)],
            Tags = ["school", "gift"],
            Discounts = new Dictionary<string, int> { ["pen"] = 10 },
        }));
        Console.WriteLine(shop.Total(new Order(2)));

        // An instance field and a void method: the exit snapshot differs from the entry snapshot.
        var counter = new Counter();
        counter.Increment(1);
        counter.Increment(2);
        counter.Increment(3);
        Console.WriteLine(counter.Count);

        // A static field and a static method.
        Console.WriteLine(MathUtil.Max(1, 2));
        Console.WriteLine(MathUtil.Max(5, 3));
        Console.WriteLine(MathUtil.Max(4, 4));
    }
}

public class Node(string name)
{
    public string Name = name;
    public Node? Next;
    public Node? Partner;
}

public class Graph
{
    public string Describe(Node node) => $"{node.Name} -> {node.Next?.Name ?? "nothing"}";
}

public record Item(string Name, decimal Price, int Quantity);

public class Order(int id)
{
    public int Id { get; } = id;
    public List<Item> Items { get; init; } = [];
    public string[] Tags { get; init; } = [];
    public Dictionary<string, int> Discounts { get; init; } = [];
}

public class Shop
{
    public decimal Total(Order order) =>
        order.Items.Sum(item => item.Price * item.Quantity * (100 - order.Discounts.GetValueOrDefault(item.Name)) / 100);
}

public class Counter
{
    int count;

    public int Count => count;

    public void Increment(int by)
    {
        count += by;
    }
}

public static class MathUtil
{
    static int calls;

    public static int Max(int a, int b)
    {
        calls++;
        if (a > b)
        {
            return a;
        }

        return b;
    }
}
