using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PhotonBypass.Infra.Database;

namespace PhotonBypass.Infra.Repository.DbContext;

internal class LocalDbContext(IOptions<LocalDapperOptions> options, IEntityEventService entity_event_service)
    : IDapperDbContext
{
    private readonly SqlConnection connection = new(options.Value.ConnectionString);

    public IDbConnection Connection => connection;

    public IEntityEventService EventService => entity_event_service;

    public Task OpenAsync()
    {
        return connection.State == ConnectionState.Open ? Task.CompletedTask : connection.OpenAsync();
    }

    public async Task<IDbTransaction> BeginTransactionAsync()
    {
        await OpenAsync();
        return await connection.BeginTransactionAsync();
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}