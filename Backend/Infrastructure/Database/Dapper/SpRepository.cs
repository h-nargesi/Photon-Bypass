using System.Data;
using Dapper;

namespace PhotonBypass.Infra.Database.Dapper;

internal class SpRepository(DapperDbContext context)
{
    private readonly IDbConnection connection = context.CreateConnection();

    protected Task<int> Execute(string spName, object? param)
    {
        if (param == null)
        {
            return connection.ExecuteAsync(spName, commandType: CommandType.StoredProcedure);
        }
        else
        {
            return connection.ExecuteAsync(spName, param, commandType: CommandType.StoredProcedure);
        }
    }

    protected Task<TEntity?> ExecuteScalarAsync<TEntity>(string spName, object? param)
    {
        if (param == null)
        {
            return connection.ExecuteScalarAsync<TEntity>(spName, commandType: CommandType.StoredProcedure);
        }
        else
        {
            return connection.ExecuteScalarAsync<TEntity>(spName, param, commandType: CommandType.StoredProcedure);
        }
    }

    protected Task<TEntity?> ExecuteWithResult<TEntity>(string spName, object? param)
    {
        if (param == null)
        {
            return connection.QueryFirstOrDefaultAsync<TEntity>(spName, commandType: CommandType.StoredProcedure);
        }
        else
        {
            return connection.QueryFirstOrDefaultAsync<TEntity>(spName, param, commandType: CommandType.StoredProcedure);
        }
    }

    protected Task<IEnumerable<TEntity>> ExecuteWithTableResult<TEntity>(string spName, object? param)
    {
        if (param == null)
        {
            return connection.QueryAsync<TEntity>(spName, commandType: CommandType.StoredProcedure);
        }
        else
        {
            return connection.QueryAsync<TEntity>(spName, param, commandType: CommandType.StoredProcedure);
        }
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}
