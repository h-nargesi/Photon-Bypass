using System.Data;
using Dapper.FastCrud;
using Dapper.FastCrud.Configuration.StatementOptions.Builders;
using PhotonBypass.Domain;

namespace PhotonBypass.Infra.Database;

public abstract class DapperRepository<TEntity>(IDapperDbContext context) : IDisposable where TEntity : class, IBaseEntity
{
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
