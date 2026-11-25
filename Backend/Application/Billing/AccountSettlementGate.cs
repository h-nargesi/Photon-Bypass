using System.Collections.Concurrent;

namespace PhotonBypass.Application.Billing;

public interface IAccountSettlementGate
{
    Task<T> RunExclusively<T>(int user_id, Func<Task<T>> operation);

    Task RunExclusively(int user_id, Func<Task> operation);
}

class AccountSettlementGate : IAccountSettlementGate
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> gates = new();

    public async Task<T> RunExclusively<T>(int user_id, Func<Task<T>> operation)
    {
        var gate = gates.GetOrAdd(user_id, static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync();

        try
        {
            return await operation();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RunExclusively(int user_id, Func<Task> operation)
    {
        await RunExclusively<object?>(user_id, async () =>
        {
            await operation();
            return null;
        });
    }
}
