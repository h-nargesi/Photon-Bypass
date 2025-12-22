namespace PhotonBypass.Test.Initializer.OutSourceManager;

internal class OutSourceLauncher(string key)
{
    private bool isInitialized;
    private Exception? exception;
    private readonly SemaphoreSlim semaphore = new(1);

    public string Key { get; } = key;

    public async Task Initialize(IOutSourceInitializer initializer)
    {
        if (exception != null) throw exception;
        if (isInitialized)
        {
            await initializer.Check(Key);
            return;
        }

        await semaphore.WaitAsync();

        if (exception != null) throw exception;
        if (isInitialized)
        {
            await initializer.Check(Key);
            return;
        }

        try
        {
            await initializer.Initialize(Key);
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
