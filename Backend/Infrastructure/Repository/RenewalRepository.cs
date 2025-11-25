using System.Data;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;
using PhotonBypass.Tools;

namespace PhotonBypass.Infra.Repository;

class RenewalRepository(LocalDbContext context) : EditableRepository<RenewalEntity>(context), IRenewalRepository
{
    public static readonly string TableName = EntityExtensions.GetTablename<RenewalEntity>();
    public static readonly string Id = EntityExtensions.GetColumnName<RenewalEntity>(x => x.Id);
    public static readonly string AccountId = EntityExtensions.GetColumnName<RenewalEntity>(x => x.AccountId);
    public static readonly string MonthLimit = EntityExtensions.GetColumnName<RenewalEntity>(x => x.MonthLimit);
    public static readonly string TrafficLimit = EntityExtensions.GetColumnName<RenewalEntity>(x => x.TrafficLimit);

    public Task<IDbTransaction> BeginTransactionAsync()
    {
        throw new NotImplementedException();
    }

    public Task Save(RenewalEntity entity)
    {
        throw new NotImplementedException();
    }

    public Task BachSave(IEnumerable<RenewalEntity> entities)
    {
        throw new NotImplementedException();
    }

    public Task<RenewalEntity?> LatestOf(int account_id)
    {
        throw new NotImplementedException();
    }

    public Task<RenewalEntity?> LatestOf(string target)
    {
        throw new NotImplementedException();
    }

    public Task<int?> LatestRealmIdOf(string target)
    {
        throw new NotImplementedException();
    }
}