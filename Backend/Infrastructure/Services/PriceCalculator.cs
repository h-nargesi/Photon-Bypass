using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PhotonBypass.Domain.Static;
using System.Reflection;

namespace PhotonBypass.Infra.Services;

class PriceCalculator(IPriceRepository repository) : IPriceCalculator
{
    private Dictionary<int, MethodInfo> Calculators = FetchCalculatorCode(repository).Result;

    public int CalculatePrice(int price_id, int users, int days, int gigabytes)
    {
        if (!Calculators.TryGetValue(price_id, out var method))
        {
            method = Calculators.Values.First();
        }

        if (method == null)
        {
            throw new Exception($"Calculator not found for price-id: {price_id}");
        }

        return (int)(method.Invoke(null, [users, days, gigabytes]) ?? 0);
    }

    public async Task UpdateCalculatorCode()
    {
        Calculators = await FetchCalculatorCode(repository);
    }

    private static async Task<Dictionary<int, MethodInfo>> FetchCalculatorCode(IPriceRepository repository)
    {
        var list = (await repository.GetLatest())
            .OrderByDescending(c => c.IsDefault)
            .ThenBy(c => c.Id)
            .Select(c => (c.Id, Method: Compile(c.CalculatorCode)))
            .ToList();

        if (list.Count < 1)
        {
            return [];
        }

        list.Add((0, list[0].Method));

        return list.ToDictionary(k => k.Id, v => v.Method);
    }

    private static MethodInfo Compile(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        var assemblyName = Path.GetRandomFileName();
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>();

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            throw new Exception("Price Caculator Error:\n" + string.Join('\n', result.Diagnostics));
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());

        var type = assembly.GetType("Calculator") ??
            throw new Exception("Price Caculator Error: The 'Calculator' class not found.");
        var method = type.GetMethod("Compute") ??
            throw new Exception("Price Caculator Error: The 'Compute' mothod not found.");

        return method;
    }
}
