using FluentAssertions;
using PhotonBypass.ServerBridge;

namespace PhotonBypass.Test.Facts.BasicFunctions.ServerBridge;

public class ResourceSynchronizationTest
{
    [Fact]
    public async Task GetToken_SameName_SecondTokenWaitsForFirstDispose()
    {
        var synchronization = new ResourceSynchronization();
        var first_entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release_first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first_holder = Task.Run(async () =>
        {
            using var token = await synchronization.GetToken("realm-1");
            first_entered.SetResult();
            await release_first.Task;
        });

        await first_entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var second_task = synchronization.GetToken("realm-1");

        // first token is still held: second acquisition must not complete
        await Task.Delay(100);
        second_task.IsCompleted.Should().BeFalse();

        release_first.SetResult();
        await first_holder.WaitAsync(TimeSpan.FromSeconds(5));

        using var second_token = await second_task.WaitAsync(TimeSpan.FromSeconds(5));
        second_token.Should().NotBeNull();
    }

    [Fact]
    public async Task GetToken_DifferentNames_AreAcquiredConcurrently()
    {
        var synchronization = new ResourceSynchronization();

        using var token_a = await synchronization.GetToken("realm-1");

        // a token for another name must not be blocked by the held token
        using var token_b = await synchronization.GetToken("realm-2").WaitAsync(TimeSpan.FromSeconds(5));

        token_a.Should().NotBeNull();
        token_b.Should().NotBeNull();
    }

    [Fact]
    public async Task GetToken_SameName_AfterDispose_CanBeReacquiredImmediately()
    {
        var synchronization = new ResourceSynchronization();

        var first = await synchronization.GetToken("realm-1");
        first.Dispose();

        using var second = await synchronization.GetToken("realm-1").WaitAsync(TimeSpan.FromSeconds(5));
        second.Should().NotBeNull();
    }
}
