using Dapper;
using Dapper.FastCrud;
using Dapper.FastCrud.Configuration.StatementOptions.Builders;
using PhotonBypass.Domain;
using PhotonBypass.Tools;

namespace PhotonBypass.Infra.Database;

public abstract class DapperRepository<TEntity>(IDapperDbContext context)
    : IDisposable where TEntity : class, IBaseEntity
{
    protected static readonly string TableName = EntityExtensions.GetTableName<TEntity>();
    protected static readonly string Id = EntityExtensions.GetColumnName<TEntity>(x => x.Id);

    protected IDapperDbContext DapperDbContext => context;

    protected async Task<IEnumerable<TEntity>> FindAsync(
        Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<TEntity>>? statement = null)
    {
        await context.OpenAsync();

        return await context.Connection.FindAsync(statement);
    }

    protected async Task<IEnumerable<T>> FindAsync<T>(
        Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<T>>? statement = null)
    {
        await context.OpenAsync();

        return await context.Connection.FindAsync(statement);
    }

    protected async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        await context.OpenAsync();

        return await context.Connection.ExecuteScalarAsync<T>(sql, param);
    }

    protected async Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param = null)
    {
        await context.OpenAsync();

        return await context.Connection.QueryAsync(sql, param);
    }

    protected async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        await context.OpenAsync();

        return await context.Connection.QueryAsync<T>(sql, param);
    }

    public void Dispose() => GC.SuppressFinalize(this);
}