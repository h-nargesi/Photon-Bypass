using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PhotonBypass.Infra.Database;

namespace PhotonBypass.Infra.Repository.DbContext;

internal class LocalDbContext(IOptions<LocalDapperOptions> options, IEntityEventService entity_event_service)
    : IDapperDbContext
{
    private readonly SqlConnection connection = new(options.Value.ConnectionString);
    private DbTransaction? currentTransaction;

    public IDbConnection Connection => connection;

    public IDbTransaction? CurrentTransaction => currentTransaction;

    public IEntityEventService EventService => entity_event_service;

    public Task OpenAsync()
    {
        return connection.State == ConnectionState.Open ? Task.CompletedTask : connection.OpenAsync();
    }

    public async Task BeginTransactionAsync()
    {
        if (currentTransaction != null)
        {
            throw new Exception("A transaction Already opened");
        }

        await OpenAsync();
        currentTransaction = await connection.BeginTransactionAsync();
    }

    public Task CommitAsync()
    {
        if (currentTransaction == null)
        {
            throw new Exception("No transaction opened");
        }

        var task = currentTransaction.CommitAsync();
        currentTransaction = null;
        return task;
    }

    public Task RollbackAsync()
    {
        if (currentTransaction == null)
        {
            throw new Exception("No transaction opened");
        }

        var task = currentTransaction.RollbackAsync();
        currentTransaction = null;
        return task;
    }

    public void Dispose()
    {
        currentTransaction?.Dispose();
        connection.Dispose();
    }
}