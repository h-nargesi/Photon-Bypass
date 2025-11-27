using Dapper.FastCrud;
using Dapper.FastCrud.Configuration.StatementOptions.Builders;
using PhotonBypass.Domain;
using PhotonBypass.Tools;
using System.Data;
using Dapper;

namespace PhotonBypass.Infra.Database;

public abstract class DapperRepository<TEntity>(IDapperDbContext context) : IDisposable where TEntity : class, IBaseEntity
{
    public static readonly string TableName = EntityExtensions.GetTablename<TEntity>();
    public static readonly string Id = EntityExtensions.GetColumnName<TEntity>(x => x.Id);

    protected IDbConnection Connection
    {
        get
        {
            if (context.Connection.State != ConnectionState.Open)
            {
                context.Connection.Open();
            }

            return context.Connection;
        }
    }

    protected Task OpenAsync()
    {
        return context.Open();
    }

    protected Task<IEnumerable<TEntity>> FindAsync(Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<TEntity>>? statement = null)
    {
        return Connection.FindAsync(statement);
    }

    protected Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        return Connection.ExecuteScalarAsync<T>(sql, param);
    }

    protected Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param = null)
    {
        return Connection.QueryAsync(sql, param);
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
