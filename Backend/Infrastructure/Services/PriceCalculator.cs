using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
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

    public async Task<int> CalculatePrice(int price_id, int users, int days, int gigabytes)
    {
        if (!initialized)
        {
            await InitializeCalculators();
        }

        var method = pool.Get(price_id);

        return (int)(method.Invoke(null, [users, days, gigabytes]) ?? 0);
    }

    private async Task InitializeCalculators()
    {
        if (initialized) return;

        await initialize_lock.WaitAsync();

        try
        {
            if (initialized) return;

            event_service.RegisterOnSave<PriceEntity>(OnPriceSaved);

            SwapPool(await FetchCalculatorCode());

            initialized = true;
        }
        finally
        {
            initialize_lock.Release();
        }
    }

    private async Task OnPriceSaved(object? sender, EntityEventArgs<PriceEntity> event_args)
    {
        try
        {
            SwapPool(await FetchCalculatorCode());
        }
        catch (Exception e)
        {
            Log.Error(e, "Price calculators refresh after price save failed.");
        }
    }

    private void SwapPool(PricePool.CalculatorGeneration generation)
    {
        var previous = pool.Set(generation);

        if (previous != null)
        {
            previous.Context.Unload();
        }
    }

    private async Task<PricePool.CalculatorGeneration> FetchCalculatorCode()
    {
        using var scope = scope_factory.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IPriceRepository>();

        var load_context = new CollectibleAssemblyLoadContext();

        var list = (await repository.GetActives())
            .OrderByDescending(c => c.IsDefault)
            .ThenBy(c => c.Id)
            .Select(c => (c.Id, Method: Compile(load_context, c.CalculatorCode)))
            .ToList();

        var methods = new Dictionary<int, MethodInfo>();

        if (list.Count > 0)
        {
            methods = list.ToDictionary(k => k.Id, v => v.Method);

            methods.Add(0, list[0].Method);
        }

        return new PricePool.CalculatorGeneration(load_context, methods);
    }

    private static MethodInfo Compile(CollectibleAssemblyLoadContext load_context, string code)
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
            throw new Exception("Price Calculator Error:\n" + string.Join('\n', result.Diagnostics));
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = load_context.LoadFromStream(ms);

        var type = assembly.GetType("Calculator") ??
                   throw new Exception("Price Calculator Error: The 'Calculator' class not found.");
        var method = type.GetMethod("Compute") ??
                     throw new Exception("Price Calculator Error: The 'Compute' method not found.");

        return method;
    }
}
