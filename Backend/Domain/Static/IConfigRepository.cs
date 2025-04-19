using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Static;

public interface IConfigRepository : IEditableRepository<ConfigEntity>
{
    Task<ConfigEntity> FindByKey(string key);
}
