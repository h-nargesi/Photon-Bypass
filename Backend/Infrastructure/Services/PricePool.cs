using System.Reflection;

namespace PhotonBypass.Infra.Services;

public class PricePool
{
    private Dictionary<int, MethodInfo>? calculators;

    public bool IsNotLoaded => calculators == null;

    public MethodInfo Get(int price_id)
    {
        if (calculators == null)
        {
            throw new Exception("Prices pool is not loaded!");
        }

        if (!calculators.TryGetValue(price_id, out var method))
        {
            method = calculators.Values.First();
        }

        return method != null
            ? method
            : throw new Exception($"Calculator not found for price-id: {price_id}");
    }

    public void Set(Dictionary<int, MethodInfo> calculator_methods)
    {
        calculators = calculator_methods;
    }
}