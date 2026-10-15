using FluentAssertions;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Services;
using System.Collections.Concurrent;

namespace PhotonBypass.Test.Facts.BasicFunctions.Infrastructure;

public class RunJobTest
{
    [Fact]
    public async Task RunJob_FansOutToEveryServer_InParallel_AndKeepsResultOrder()
    {
        var servers = Enumerable.Range(1, 5)
            .Select(id => new ServerEntity { Id = id, Name = $"srv-{id}" })
            .ToArray();

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new ConcurrentBag<int>();

        var job = servers.RunJob(async server =>
        {
            calls.Add(server.Id);
            await entered.Task;
            return server.Id * 10;
        });

        // every function must have started (and be awaiting) before the gate opens,
        // proving the fan-out overlaps instead of running serially
        calls.Should().HaveCount(servers.Length);
        entered.SetResult();

        var results = await job.WaitAsync(TimeSpan.FromSeconds(5));

        results.Should().Equal([10, 20, 30, 40, 50]);
    }

    [Fact]
    public async Task RunJob_VoidOverload_RunsEveryServerToCompletion()
    {
        var servers = Enumerable.Range(1, 3)
            .Select(id => new ServerEntity { Id = id, Name = $"srv-{id}" })
            .ToArray();

        var calls = new ConcurrentBag<int>();

        await servers.RunJob(async server =>
        {
            await Task.Yield();
            calls.Add(server.Id);
        });

        calls.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task RunJob_WhenAServerTaskThrows_SurfacesErrorAfterAllServersComplete()
    {
        var servers = Enumerable.Range(1, 3)
            .Select(id => new ServerEntity { Id = id, Name = $"srv-{id}" })
            .ToArray();

        var calls = new ConcurrentBag<int>();

        var act = () => servers.RunJob(async server =>
        {
            await Task.Yield();
            calls.Add(server.Id);
            if (server.Id == 2) throw new Exception($"boom on {server.Name}");
            return server.Id;
        }).WaitAsync(TimeSpan.FromSeconds(5));

        await act.Should().ThrowAsync<Exception>().WithMessage("boom on srv-2");

        // Task.WhenAll waits for every server task even when one fails
        calls.Should().BeEquivalentTo([1, 2, 3]);
    }
}
