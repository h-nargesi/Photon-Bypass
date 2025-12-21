namespace PhotonBypass.Test.Initializer;

internal static class OutSourceManager
{
    private static readonly Dictionary<string, KeyInitialManager> keys = [];

    public static Task InitializeOutSource<TInitializer>(this IServiceScope scope, string key)
        where TInitializer : IOutSourceInitializer
    {
        KeyInitialManager? initializer;

        lock (keys)
        {
            if (!keys.TryGetValue(key, out initializer))
            {
                keys.Add(key, initializer = new KeyInitialManager());
            }
        }

        return initializer.Initialize<TInitializer>(scope, key);
    }

    public static Task ClearAll()
    {
        Task[] tasks;
        lock (keys)
        {
            tasks = keys.Select(pair => pair.Value.Clear())
                .ToArray();
        }

        return Task.WhenAll(tasks);
    }

    private class KeyInitialManager
    {
        private readonly Dictionary<Type, ISynchronizationInitializeManager> initializers = [];

        public Task Initialize<TInitializer>(IServiceScope scope, string key) where TInitializer : IOutSourceInitializer
        {
            ISynchronizationInitializeManager? initializer;

            lock (initializers)
            {
                if (!initializers.TryGetValue(typeof(TInitializer), out initializer))
                {
                    initializers.Add(typeof(TInitializer),
                        initializer = new SynchronizationInitializeManager<TInitializer>(key));
                }
            }

            return initializer.Initialize(scope);
        }

        public Task Clear()
        {
            Task[] tasks;
            lock (initializers)
            {
                tasks = initializers.Select(pair => pair.Value.Clear())
                    .ToArray();
            }

            return Task.WhenAll(tasks);
        }
    }

    private interface ISynchronizationInitializeManager
    {
        Task Initialize(IServiceScope scope);

        Task Clear();
    }

    private class SynchronizationInitializeManager<TInitializer>(string key)
        : ISynchronizationInitializeManager where TInitializer : IOutSourceInitializer
    {
        private bool isInitialized;
        private Exception? exception;
        private readonly SemaphoreSlim semaphore = new(1);
        private TInitializer? initializer;

        public async Task Initialize(IServiceScope scope)
        {
            if (exception != null) throw exception;

            initializer = scope.ServiceProvider.GetRequiredService<TInitializer>();

            if (isInitialized)
            {
                await initializer.Check(key);
                return;
            }

            await semaphore.WaitAsync();

            if (exception != null) throw exception;
            if (isInitialized)
            {
                await initializer.Check(key);
                return;
            }

            try
            {
                await initializer.Initialize(key);
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

        public Task Clear()
        {
            return initializer?.Clear(key) ?? Task.CompletedTask;
        }
    }
}

internal interface IOutSourceInitializer
{
    Task Initialize(string key);

    Task Check(string key);

    Task Clear(string key);
}