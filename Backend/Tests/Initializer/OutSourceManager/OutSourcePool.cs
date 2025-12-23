namespace PhotonBypass.Test.Initializer.OutSourceManager;

internal static class OutSourcePool
{
    private static readonly LauncherDict Launchers = [];

    public static Task Register<TInitializer>(this IServiceScope provider, string key, object service) where TInitializer : IOutSourceInitializer
    {
        OutSourceLauncherNode? node;
        lock (Launchers)
        {
            if (!Launchers.TryGetValue((key, typeof(TInitializer)), out node))
            {
                Launchers.Add((key, typeof(TInitializer)), node = new OutSourceLauncherNode
                {
                    SampleInitializer = provider.ServiceProvider.GetRequiredService<TInitializer>(),
                    Launcher = new OutSourceLauncher(key)
                });
            }

            node.Services.Add(service);
        }

        return node.Launcher.Initialize(provider.ServiceProvider.GetRequiredService<TInitializer>());
    }

    private sealed class LauncherDict : Dictionary<(string word, Type type), OutSourceLauncherNode>
    {
        ~LauncherDict()
        {
            Task.WaitAll([.. this.Select(pair => pair.Value.SampleInitializer.Clear(pair.Key.word))]);
        }
    }

    private class OutSourceLauncherNode
    {
        public IOutSourceInitializer SampleInitializer { get; set; } = null!;

        public OutSourceLauncher Launcher { get; init; } = null!;

        public HashSet<object> Services { get; } = [];
    }
}
