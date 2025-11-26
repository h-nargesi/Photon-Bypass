using Dapper.FastCrud;
using Dapper.FastCrud.Configuration.StatementOptions.Builders;
using PhotonBypass.Domain;
using PhotonBypass.Tools;
using System.Data;

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

    protected Task<IEnumerable<TEntity>> FindAsync(Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<TEntity>>? statementOptions = null)
    {
        return Connection.FindAsync(statementOptions);
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
