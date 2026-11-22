using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Repository;
using PhotonBypass.Domain.Static;
using PhotonBypass.Infra.Database;
using Serilog;

namespace PhotonBypass.Infra.Services;

class PriceCalculator(PricePool pool, IEntityEventService event_service, IServiceScopeFactory scope_factory)
    : IPriceCalculator
{
    private readonly SemaphoreSlim initialize_lock = new(1, 1);

    private volatile bool initialized;

    public async Task<PriceCalculationOutput> Calculate(PriceCalculationInput input)
    {
        if (!initialized)
        {
            await InitializeCalculators();
        }

        var method = pool.Get(input.PlanId);

        return method(input) ?? throw new Exception(
            $"Calculator of plan-id {input.PlanId} returned no result.");
    }

    public async Task Reload()
    {
        // out-of-band edits (direct SQL) raise no entity event — this is the
        // explicit admin path (Docs/24 D4); first-call initialization is
        // idempotent under the same lock
        await InitializeCalculators(force: true);
    }

    public void ValidateCode(string code)
    {
        var load_context = new CollectibleAssemblyLoadContext();

        try
        {
            Compile(load_context, code);
        }
        finally
        {
            load_context.Unload();
        }
    }

    private async Task InitializeCalculators(bool force = false)
    {
        if (initialized && !force) return;

        await initialize_lock.WaitAsync();

        try
        {
            if (initialized && !force) return;

            event_service.RegisterOnSave<PlanEntity>(OnPlanSaved);
            event_service.RegisterOnDelete<PlanEntity>(OnPlanSaved);

            SwapPool(await FetchCalculatorCode());

            initialized = true;
        }
        finally
        {
            initialize_lock.Release();
        }
    }

    private async Task OnPlanSaved(object? sender, EntityEventArgs<PlanEntity> event_args)
    {
        try
        {
            SwapPool(await FetchCalculatorCode());
        }
        catch (Exception e)
        {
            Log.Error(e, "Plan calculators refresh after plan save failed.");
        }
    }

    private void SwapPool(PricePool.CalculatorGeneration generation)
    {
        var previous = pool.Swap(generation);

        if (previous != null)
        {
            previous.Context.Unload();
        }
    }

    private async Task<PricePool.CalculatorGeneration> FetchCalculatorCode()
    {
        using var scope = scope_factory.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IPlanRepository>();

        var load_context = new CollectibleAssemblyLoadContext();

        var methods = new Dictionary<int, PricePool.CalculatorMethod>();

        foreach (var plan in await repository.GetActives())
        {
            methods.Add(plan.Id, Compile(load_context, plan.CalculatorCode));
        }

        return new PricePool.CalculatorGeneration(load_context, methods);
    }

    /// <summary>
    /// Compiles one snippet against the host assemblies (all loaded,
    /// non-dynamic — the shared DTO types resolve to the host's own, Docs/24
    /// §4) and binds the mandatory
    /// <c>public static PriceCalculationOutput Compute(PriceCalculationInput)</c>
    /// method of the mandatory <c>Calculator</c> class into a typed delegate.
    /// </summary>
    private static PricePool.CalculatorMethod Compile(CollectibleAssemblyLoadContext load_context, string code)
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
            load_context.Unload();
            throw new Exception("Plan Calculator Error:\n" + string.Join('\n', result.Diagnostics));
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = load_context.LoadFromStream(ms);

        var type = assembly.GetType("Calculator") ??
                   throw new Exception("Plan Calculator Error: The 'Calculator' class not found.");

        var method = type.GetMethod("Compute", [typeof(PriceCalculationInput)]) ??
                     throw new Exception(
                         "Plan Calculator Error: The 'Compute(PriceCalculationInput)' method not found.");

        if (!typeof(PriceCalculationOutput).IsAssignableFrom(method.ReturnType))
        {
            throw new Exception(
                "Plan Calculator Error: 'Compute' must return 'PriceCalculationOutput' (Docs/24 D2).");
        }

        return method.CreateDelegate<PricePool.CalculatorMethod>();
    }
}
