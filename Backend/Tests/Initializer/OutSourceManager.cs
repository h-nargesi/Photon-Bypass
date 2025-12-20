namespace PhotonBypass.Test.Initializer;

internal class OutSourceManager(IServiceProvider services) : IOutSourceLevelService
{
    private readonly Dictionary<string, KeyInitialManager> keys = [];

    public Task InitializeOutSource<TInitializer>(string key) where TInitializer : IOutSourceInitializer
    {
        KeyInitialManager? initializer;

        lock (keys)
        {
            if (!keys.TryGetValue(key, out initializer))
            {
                keys.Add(key, initializer = new KeyInitialManager(services));
            }
        }

        return initializer.Initialize<TInitializer>(key);
    }

    private class KeyInitialManager(IServiceProvider services)
    {
        private readonly Dictionary<Type, SynchronizationInitializeManager> initializers = [];

        public Task Initialize<TInitializer>(string key) where TInitializer : IOutSourceInitializer
        {
            if (initializers.TryGetValue(typeof(TInitializer), out var initializer) || initializer == null)
            {
                initializers[typeof(TInitializer)] = initializer = new SynchronizationInitializeManager(services);
            }

            return initializer.Initialize<TInitializer>(key);
        }
    }

    private class SynchronizationInitializeManager(IServiceProvider services)
    {
        private bool isInitialized;
        private Exception? exception;
        private readonly SemaphoreSlim semaphore = new(0);

        public async Task Initialize<TInitializer>(string key) where TInitializer : IOutSourceInitializer
        {
            if (exception != null) throw exception;
            if (isInitialized) return;

            await semaphore.WaitAsync();

            if (exception != null) throw exception;
            if (isInitialized) return;

            try
            {
                await services.GetRequiredService<TInitializer>()
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