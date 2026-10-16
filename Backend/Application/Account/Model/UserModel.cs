using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Application.Account.Model;

public class UserModel : TargetModel
{
    public string? Picture { get; set; }

    public decimal Balance { get; set; }

    public AccountRole Role { get; set; }

    public bool AllowSelfService { get; set; }

    public IDictionary<string, TargetModel> TargetArea { get; set; } = null!;
}
