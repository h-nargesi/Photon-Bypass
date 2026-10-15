using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace PhotonBypass.Xui.Application;

[DisallowConcurrentExecution]
public class XuiTrafficCollector(IServiceProvider service_provider) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = service_provider.CreateScope();

        var sync = scope.ServiceProvider.GetRequiredService<IXuiTrafficSync>();

        await sync.Run();
    }
}
