namespace PhotonBypass.Test.Initializer;

internal static class OutSourceManager
{
    private static readonly Dictionary<string, KeyInitialManager> keys = [];

    public static Task InitializeOutSource<TInitializer>(this IServiceScope scope, string key) where TInitializer : IOutSourceInitializer
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

    private class KeyInitialManager
    {
        private readonly Dictionary<Type, SynchronizationInitializeManager> initializers = [];

        public Task Initialize<TInitializer>(IServiceScope scope, string key) where TInitializer : IOutSourceInitializer
        {
            SynchronizationInitializeManager? initializer;

            lock (initializers)
            {
                if (!initializers.TryGetValue(typeof(TInitializer), out initializer))
                {
                    initializers.Add(typeof(TInitializer), initializer = new SynchronizationInitializeManager());
                }
            }

            return initializer.Initialize<TInitializer>(scope, key);
        }
    }

    private class SynchronizationInitializeManager()
    {
        private bool isInitialized;
        private Exception? exception;
        private readonly SemaphoreSlim semaphore = new(1);

        public async Task Initialize<TInitializer>(IServiceScope scope, string key) where TInitializer : IOutSourceInitializer
        {
            if (exception != null) throw exception;

            var initializer = scope.ServiceProvider.GetRequiredService<TInitializer>();

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
    }
}

internal interface IOutSourceInitializer
{
    Task Initialize(string key);

    Task Check(string key);
}