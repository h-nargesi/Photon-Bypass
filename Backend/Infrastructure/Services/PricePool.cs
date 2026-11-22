using System.Runtime.Loader;
using PhotonBypass.Domain.Static;

namespace PhotonBypass.Infra.Services;

public sealed class CollectibleAssemblyLoadContext : AssemblyLoadContext
{
    public CollectibleAssemblyLoadContext() : base(isCollectible: true)
    {
    }
}

/// <summary>
/// Holds one immutable generation of compiled plan calculators (Docs/24 D2/D4).
/// The swap is atomic — the old pool had a torn-read window between
/// <c>Set</c> and <c>Get</c> (plain field, no fence), which the redesign fixes
/// with <see cref="System.Threading.Interlocked"/>/<see cref="System.Threading.Volatile"/>.
/// </summary>
public class PricePool
{
    private CalculatorGeneration? current;

    public CalculatorMethod Get(int plan_id)
    {
        var generation = Volatile.Read(ref current) ?? throw new Exception("Calculator pool is not loaded!");

        if (!generation.Methods.TryGetValue(plan_id, out var method))
        {
            throw new Exception($"Calculator not found for plan-id: {plan_id}");
        }

        return method;
    }

    public CalculatorGeneration? Swap(CalculatorGeneration generation)
    {
        // full fence — readers via Volatile.Read never observe a torn generation
        return Interlocked.Exchange(ref current, generation);
    }

    /// <summary>Strongly-typed entry of the compiled <c>Calculator.Compute</c> method.</summary>
    public delegate PriceCalculationOutput CalculatorMethod(PriceCalculationInput input);

    public sealed class CalculatorGeneration(CollectibleAssemblyLoadContext context,
        Dictionary<int, CalculatorMethod> methods)
    {
        public CollectibleAssemblyLoadContext Context { get; } = context;

        public Dictionary<int, CalculatorMethod> Methods { get; } = methods;
    }
}
