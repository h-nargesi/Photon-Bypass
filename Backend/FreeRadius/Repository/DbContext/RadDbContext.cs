using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using PhotonBypass.Infra.Database;
using System.Data;

namespace PhotonBypass.FreeRadius.Repository.DbContext;

class RadDbContext(IOptions<RadDapperOptions> options, IEntityEventService entity_event_service) : IDapperDbContext
{
    private readonly MySqlConnection connection = new(options.Value.ConnectionString);
    private MySqlTransaction? currentTransaction;

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

        Task task;
        lock (currentTransaction)
        {
            task = currentTransaction.CommitAsync();
            currentTransaction = null;
        }
        return task;
    }

    public Task RollbackAsync()
    {
        if (currentTransaction == null)
        {
            throw new Exception("No transaction opened");
        }

        Task task;
        lock (currentTransaction)
        {
            task = currentTransaction.RollbackAsync();
            currentTransaction = null;
        }
        return task;
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}