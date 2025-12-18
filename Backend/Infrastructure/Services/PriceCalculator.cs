using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PhotonBypass.Domain.Static;

namespace PhotonBypass.Infra.Services;

class PriceCalculator(Lazy<IPriceRepository> repository) : IPriceCalculator
{
    private Dictionary<int, MethodInfo>? calculators;

    public int CalculatePrice(int price_id, int users, int days, int gigabytes)
    {
        calculators ??= InitializeCalculators().Result;

        if (!calculators.TryGetValue(price_id, out var method))
        {
            method = calculators.Values.First();
        }

        if (method == null)
        {
            throw new Exception($"Calculator not found for price-id: {price_id}");
        }

        return (int)(method.Invoke(null, [users, days, gigabytes]) ?? 0);
    }

    private Task<Dictionary<int, MethodInfo>> InitializeCalculators()
    {
        repository.Value.Events.OnSave += async (_, _) => calculators = await FetchCalculatorCode();
        return FetchCalculatorCode();
    }

    private async Task<Dictionary<int, MethodInfo>> FetchCalculatorCode()
    {
        var list = (await repository.Value.GetActives())
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
        var syntax_tree = CSharpSyntaxTree.ParseText(code);

        var assembly_name = Path.GetRandomFileName();
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>();

        var compilation = CSharpCompilation.Create(
            assembly_name,
            [syntax_tree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            throw new Exception("Price Calculator Error:\n" + string.Join('\n', result.Diagnostics));
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());

        var type = assembly.GetType("Calculator") ??
                   throw new Exception("Price Calculator Error: The 'Calculator' class not found.");
        var method = type.GetMethod("Compute") ??
                     throw new Exception("Price Calculator Error: The 'Compute' method not found.");

        return method;
    }
}