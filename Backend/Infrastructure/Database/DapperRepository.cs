using Dapper;
using Dapper.FastCrud;
using Dapper.FastCrud.Configuration.StatementOptions.Builders;
using PhotonBypass.Domain;
using PhotonBypass.Tools;
using Z.Dapper.Plus;

namespace PhotonBypass.Infra.Database;

public abstract class DapperRepository<TEntity>(IDapperDbContext context)
    : IDisposable where TEntity : class, IBaseEntity
{
    protected static readonly string TableName = EntityExtensions.GetTableName<TEntity>();
    protected static readonly string Id = EntityExtensions.GetColumnName<TEntity>(x => x.Id);

    static DapperRepository()
    {
        DapperPlusManager.Entity<TEntity>().Table(TableName).Identity(x => x.Id, true);
    }

    protected IDapperDbContext DapperDbContext => context;

    protected async Task<IEnumerable<TEntity>> FindAsync(
        Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<TEntity>>? statement = null)
    {
        await context.OpenAsync();

        statement = CheckTransaction(statement);

        return await context.Connection.FindAsync(statement);
    }

    protected async Task<IEnumerable<T>> FindAsync<T>(
        Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<T>>? statement = null)
    {
        await context.OpenAsync();

        statement = CheckTransaction(statement);

        return await context.Connection.FindAsync(statement);
    }

    protected async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        await context.OpenAsync();

        return await context.Connection.ExecuteScalarAsync<T>(sql, param, context.CurrentTransaction);
    }

    protected async Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param = null)
    {
        await context.OpenAsync();

        return await context.Connection.QueryAsync(sql, param, context.CurrentTransaction);
    }

    protected async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        await context.OpenAsync();

        return await context.Connection.QueryAsync<T>(sql, param, context.CurrentTransaction);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    private Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<T>>? CheckTransaction<T>(
        Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<T>>? statement = null)
    {
        if (context.CurrentTransaction == null) return statement;
        
        var arg_statement = statement;
        statement = st =>
        {
            arg_statement?.Invoke(st);
            st.AttachToTransaction(context.CurrentTransaction);
        };

        return statement;
    }
}