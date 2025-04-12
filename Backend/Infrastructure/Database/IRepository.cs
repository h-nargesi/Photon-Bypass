using Dapper.FastCrud.Configuration.StatementOptions.Builders;
namespace PhotonBypass.Infra.Database;

public interface IRepository<TEntity> : IDisposable
    where TEntity : class, IBaseEntity
{
    public Task<TEntity?> FindAsync(TEntity entity);

    public Task<IEnumerable<TEntity>> FindAsync(Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<TEntity>>? statementOptions = null);

    public Task<int> AddAsync(TEntity entity);

    public Task<bool> UpdateAsync(TEntity entity);

    public Task<bool> DeleteAsync(TEntity entity);

    public Task<int> BulkDeleteAsync(FormattableString whereClause, object? parameters);

    public Task<IEnumerable<TEntity>> QueryAsync(string sql, object? param = null);

    public Task<int> ExecuteAsync(string sql, object? param = null);
}
