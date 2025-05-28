using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Static;
using System.Reflection;

namespace PhotonBypass.Infra.Services;

public class PriceCalculator(IPriceRepository repository) : IPriceCalculator
{
    private readonly Dictionary<PlanType, MethodInfo> Calculators = FetchCalculatorCode(repository);

    public int CalculatePrice(PlanType type, int users, int value)
    {
        if (Calculators.TryGetValue(type, out var method))
        {
            return (int)(method.Invoke(null, [users, value]) ?? 0);
        }

        throw new Exception($"Calculator not found for type: {type}");
    }

    private static Dictionary<PlanType, MethodInfo> FetchCalculatorCode(IPriceRepository repository)
    {
        var list = repository.GetLeatest().Result;

        return list.ToDictionary(k => k.PlanType, v => Compile(v.CalculatorCode));
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
