using System.Data;
using Dapper;
using Dapper.FastCrud;
using Dapper.FastCrud.Configuration.StatementOptions.Builders;

namespace PhotonBypass.Infra.Database.Dapper;

internal class DapperRepository<TEntity, TDtoEntity, TId>(DapperDbContext context) : IDisposable
    where TEntity : class, IBaseEntity where TDtoEntity : class where TId : notnull
{
    private readonly IDbConnection connection = context.CreateConnection();

    public Task<TDtoEntity?> FindAsync(TDtoEntity entity)
    {
        return connection.GetAsync(entity);
    }

    public async Task<IReadOnlyList<TDtoEntity>> GetAllAsync()
    {
        return (await connection.FindAsync<TDtoEntity>()).ToList();
    }

    protected Task<IEnumerable<TDtoEntity>> QueryAsync(string sql, object? param = null)
    {
        return connection.QueryAsync<TDtoEntity>(sql, param);
    }

    public async Task<int> AddAsync(TEntity entity)
    {
        await connection.InsertAsync(entity);
        return entity.Id;
    }

    public Task<bool> UpdateAsync(TEntity entity)
    {
        return connection.UpdateAsync(entity);
    }

    public Task<bool> DeleteAsync(TEntity entity)
    {
        return connection.DeleteAsync(entity);
    }

    protected Task<int> BulkDeleteAsync(FormattableString whereClause, object? parameters)
    {
        return connection.BulkDeleteAsync<TEntity>(statement =>
            statement.Where(whereClause).WithParameters(parameters));
    }

    protected Task<int> ExecuteAsync(string sql, object? param = null)
    {
        return connection.ExecuteAsync(sql, param);
    }

    protected Task<IEnumerable<TDtoEntity>> FindAsync(Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<TDtoEntity>>? statementOptions = null)
    {
        return connection.FindAsync(statementOptions);
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}
