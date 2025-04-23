using System.Data;
using Dapper.FastCrud;
using Dapper.FastCrud.Configuration.StatementOptions.Builders;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Infra.Database;

public abstract class DapperRepository<TEntity>(DapperDbContext context) : IRepository, IDisposable where TEntity : class, IBaseEntity
{
    protected readonly IDbConnection connection = context.CreateConnection();

    public IDbTransaction BeginTransaction()
    {
        return connection.BeginTransaction();
    }

    protected Task<IEnumerable<TEntity>> FindAsync(Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<TEntity>>? statementOptions = null)
    {
        return connection.FindAsync(statementOptions);
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
