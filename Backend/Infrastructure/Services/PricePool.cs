using System.Reflection;
using System.Runtime.Loader;

namespace PhotonBypass.Infra.Services;

public sealed class CollectibleAssemblyLoadContext : AssemblyLoadContext
{
    public CollectibleAssemblyLoadContext() : base(isCollectible: true)
    {
    }
}

public class PricePool
{
    private CalculatorGeneration? current;

    public MethodInfo Get(int price_id)
    {
        var generation = current ?? throw new Exception("Prices pool is not loaded!");

        if (!generation.Methods.TryGetValue(price_id, out var method))
        {
            method = generation.Methods.Values.First();
        }

        return method != null
            ? method
            : throw new Exception($"Calculator not found for price-id: {price_id}");
    }

    public CalculatorGeneration? Set(CalculatorGeneration generation)
    {
        var previous = current;
        current = generation;
        return previous;
    }

    public sealed class CalculatorGeneration(CollectibleAssemblyLoadContext context,
        Dictionary<int, MethodInfo> methods)
    {
        public CollectibleAssemblyLoadContext Context { get; } = context;

        public Dictionary<int, MethodInfo> Methods { get; } = methods;
    }
}
