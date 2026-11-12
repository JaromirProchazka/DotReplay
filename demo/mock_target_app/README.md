# Demo apps for .NET

C# / .NET 8 ports of the Java Auto-debugger demo apps in Auto-debugger `demo/apps/`. Each one is a target for the .NET version of the auto-debugger and does the same thing as its Java original, so its pipeline output can be compared with `demo/mock_data/<scenario>/`.

| Folder | Java original | Target method (.NET) | Mock data |
|---|---|---|---|
| `args/` | `apps/args` (`calc-app.jar`) | `Com.Example.Calculator.Add(int, int)` | `mock_data/args/` |

## Build and run

```bash
cd demo/mock_target_app/args
dotnet build -c Debug        # -> bin/Debug/net8.0/calc-app.dll
dotnet run                   # or: dotnet bin/Debug/net8.0/calc-app.dll
```

Expected output, the same as `java -jar demo/calc-app.jar`:

```
3
7
11
```

## Java to C# mapping

- **Names:** package `com.example` becomes namespace `Com.Example`, and methods use .NET PascalCase (`add` becomes `Add`). Classes keep their names: `CalcApp` and `Calculator`.
- **Entry point:** the jar's `Main-Class` maps to `<StartupObject>` in the `.csproj`. The jar name `calc-app` maps to `<AssemblyName>`.
- **`int` arithmetic:** C# `int` is 32-bit and unchecked by default, so overflow wraps just like Java `int`.
