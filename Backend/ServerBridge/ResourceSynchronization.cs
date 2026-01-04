namespace PhotonBypass.ServerBridge;

public class ResourceSynchronization
{
    private readonly SemaphoreSlim manager = new(1);
    private readonly Dictionary<string, SemaphoreSlim> resources = [];

    public async Task<Token> GetToken(string name)
    {
        SemaphoreSlim? semaphore;

        try
        {
            await manager.WaitAsync();

            if (!resources.TryGetValue(name, out semaphore))
            {
                resources.Add(name, semaphore = new SemaphoreSlim(1));
            }
        }
        finally
        {
            manager.Release();
        }

        await semaphore.WaitAsync();

        return new Token(semaphore);
    }

    public class Token(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose()
        {
            semaphore.Release();

            GC.SuppressFinalize(this);
        }
    }
}
