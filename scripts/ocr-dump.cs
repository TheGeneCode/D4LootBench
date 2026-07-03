#:project src/D4LootBench.Vision/D4LootBench.Vision.csproj
#:property TargetFramework=net10.0-windows10.0.19041.0

using D4LootBench.Vision;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotnet run ocr-dump.cs -- <screenshot.png>");
    return 1;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    return 1;
}

await using var fs = File.OpenRead(path);
var reader = new WindowsOcrGearReader();
var lines = await reader.ReadLinesAsync(fs);

foreach (var line in lines)
{
    Console.WriteLine(line);
}

return 0;
