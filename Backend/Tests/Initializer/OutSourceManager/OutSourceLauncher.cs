namespace PhotonBypass.Test.Initializer.OutSourceManager;

internal class OutSourceLauncher(string key)
{
    private bool isInitialized;
    private Exception? exception;
    private readonly SemaphoreSlim semaphore = new(1);

    public async Task Initialize(IOutSourceInitializer initializer)
    {
        if (exception != null) throw exception;
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
