namespace PhotonBypass.Test.Tools;

public static class OutSourceHandler
{
    private static bool is_initilized = false;
    private static readonly SemaphoreSlim semaphote = new(0);

    public static async Task Initialize()
    {
        if (is_initilized) return;

        await semaphote.WaitAsync();

        if (is_initilized) return;

        await InitializeOptions();

        Task.WaitAll(
            InitializeLocalDatabase(),
            InitializeMikrotik());

        is_initilized = true;
        semaphote.Release(int.MaxValue);
    }

    private static async Task InitializeOptions()
    {
    }

    private static async Task InitializeLocalDatabase()
    {
    }

    private static async Task InitializeMikrotik()
    {
    }
}
