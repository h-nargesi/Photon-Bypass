namespace PhotonBypass.Test.Initializer;

internal class OutSourceManager : IOutSourceLevelService
{
    private readonly Dictionary<string, KeyInitialManager> keys = [];

    public Task InitializeOutSource<TInitializer>(IServiceScope scope, string key) where TInitializer : IOutSourceInitializer
    {
        KeyInitialManager? initializer;

        lock (keys)
        {
            if (!keys.TryGetValue(key, out initializer))
            {
                keys.Add(key, initializer = new KeyInitialManager(scope));
            }
        }

        return initializer.Initialize<TInitializer>(key);
    }

    private class KeyInitialManager(IServiceScope scope)
    {
        private readonly Dictionary<Type, SynchronizationInitializeManager> initializers = [];

        public Task Initialize<TInitializer>(string key) where TInitializer : IOutSourceInitializer
        {
            if (initializers.TryGetValue(typeof(TInitializer), out var initializer) || initializer == null)
            {
                initializers[typeof(TInitializer)] = initializer = new SynchronizationInitializeManager(scope);
            }

            return initializer.Initialize<TInitializer>(key);
        }
    }

    private class SynchronizationInitializeManager(IServiceScope scope)
    {
        private bool isInitialized;
        private Exception? exception;
        private readonly SemaphoreSlim semaphore = new(1);

        public async Task Initialize<TInitializer>(string key) where TInitializer : IOutSourceInitializer
        {
            if (exception != null) throw exception;
            if (isInitialized) return;

            await semaphore.WaitAsync();

            if (exception != null) throw exception;
            if (isInitialized) return;

            try
            {
                await scope.ServiceProvider.GetRequiredService<TInitializer>()
                    .Initialize(key);
                isInitialized = true;
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                semaphore.Release(int.MaxValue);
            }
        }
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton<OutSourceManager>();
    }
}

internal interface IOutSourceInitializer
{
    Task Initialize(string key);
}