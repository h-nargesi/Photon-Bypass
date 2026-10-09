using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class TransactionalMockDbContext : IDbContext, IUnitLevelService
{
    private readonly Stack<Action> undo_stack = new();
    private bool in_transaction;

    public Task BeginTransactionAsync()
    {
        if (in_transaction)
        {
            throw new Exception("A transaction Already opened");
        }

        in_transaction = true;
        return Task.CompletedTask;
    }

    public Task CommitAsync()
    {
        if (!in_transaction)
        {
            throw new Exception("No transaction opened");
        }

        undo_stack.Clear();
        in_transaction = false;
        return Task.CompletedTask;
    }

    public Task RollbackAsync()
    {
        if (!in_transaction)
        {
            throw new Exception("No transaction opened");
        }

        while (undo_stack.Count > 0)
        {
            undo_stack.Pop()();
        }

        in_transaction = false;
        return Task.CompletedTask;
    }

    public void RegisterUndo(Action undo)
    {
        if (in_transaction)
        {
            undo_stack.Push(undo);
        }
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<TransactionalMockDbContext>();
    }
}
