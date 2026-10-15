using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Xui.Application;
using Quartz;
using System.Reflection;
using Mock = Moq.Mock;

namespace PhotonBypass.Test.Facts.Xui;

public class XuiTrafficCollectorTest : UnitLevelServiceInitializer
{
    private readonly Mock<IXuiTrafficSync> _syncMoq = new();

    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddTransient(_ => _syncMoq.Object);
    }

    [Fact]
    public async Task Execute_ResolvesScopedSyncAndRunsItExactlyOnce()
    {
        _syncMoq.Setup(sync => sync.Run()).Returns(Task.CompletedTask);

        var collector = new XuiTrafficCollector(App.Services);
        await collector.Execute(Moq.Mock.Of<IJobExecutionContext>());

        _syncMoq.Verify(sync => sync.Run(), Times.Once);
    }

    [Fact]
    public void XuiTrafficCollector_IsRegisteredAsNonConcurrentQuartzJob()
    {
        typeof(XuiTrafficCollector).GetCustomAttribute<DisallowConcurrentExecutionAttribute>()
            .Should().NotBeNull("traffic collection must never overlap with itself");
    }
}
