using Dapper;
using Dapper.FastCrud;
using Dapper.FastCrud.Configuration.StatementOptions.Builders;
using PhotonBypass.Domain;
using System.Data;

namespace PhotonBypass.Infra.Database.Dapper;

abstract class DapperRepository<TEntity>(DapperDbContext context) : IRepository<TEntity>, IDisposable where TEntity : class, IBaseEntity
{
    private readonly IDbConnection connection = context.CreateConnection();

    public Task<TEntity?> FindAsync(TEntity entity)
    {
        return connection.GetAsync(entity);
    }

    public Task<IEnumerable<TEntity>> FindAsync(Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<TEntity>>? statementOptions = null)
    {
        return connection.FindAsync(statementOptions);
    }

    public Task<IEnumerable<TEntity>> GetAllAsync()
    {
        return connection.FindAsync<TEntity>();
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

    public Task<int> BulkDeleteAsync(FormattableString whereClause, object? parameters)
    {
        return connection.BulkDeleteAsync<TEntity>(statement => statement
            .Where(whereClause)
            .WithParameters(parameters));
    }

    public Task<int> ExecuteAsync(string sql, object? param = null)
    {
        return connection.ExecuteAsync(sql, param);
    }

    public Task<IEnumerable<TEntity>> QueryAsync(string sql, object? param = null)
    {
        return connection.QueryAsync<TEntity>(sql, param);
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
