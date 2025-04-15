using PhotonBypass.Infra.Database;

namespace PhotonBypass.Domain.Local;

public class ResetPassEntity : IBaseEntity
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    public DateTime ExpireDate { get; set; }

    public string HashCode { get; set; } = null!;
}
