using Dapper;
using Dapper.FastCrud;
using Dapper.FastCrud.Configuration.StatementOptions.Builders;
using PhotonBypass.Domain;
using System.Data;

namespace PhotonBypass.Infra.Database;

public abstract class DapperRepository<TEntity>(DapperDbContext context) : IDisposable where TEntity : class, IBaseEntity
{
    private readonly IDbConnection connection = context.CreateConnection();

    protected Task<TEntity?> FindAsync(TEntity entity)
    {
        return connection.GetAsync(entity);
    }

    protected Task<IEnumerable<TEntity>> FindAsync(Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<TEntity>>? statementOptions = null)
    {
        return connection.FindAsync(statementOptions);
    }

    protected Task<IEnumerable<TEntity>> GetAllAsync()
    {
        return connection.FindAsync<TEntity>();
    }

    protected async Task<int> AddAsync(TEntity entity)
    {
        await connection.InsertAsync(entity);
        return entity.Id;
    }

    protected Task<bool> UpdateAsync(TEntity entity)
    {
        return connection.UpdateAsync(entity);
    }

    protected Task<bool> DeleteAsync(TEntity entity)
    {
        return connection.DeleteAsync(entity);
    }

    protected Task<int> BulkDeleteAsync(FormattableString whereClause, object? parameters)
    {
        return connection.BulkDeleteAsync<TEntity>(statement => statement
            .Where(whereClause)
            .WithParameters(parameters));
    }

    protected Task<int> ExecuteAsync(string sql, object? param = null)
    {
        return connection.ExecuteAsync(sql, param);
    }

    protected Task<IEnumerable<TEntity>> QueryAsync(string sql, object? param = null)
    {
        return connection.QueryAsync<TEntity>(sql, param);
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
