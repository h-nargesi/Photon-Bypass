namespace PhotonBypass.Application.Account.Model;

public class UserModel : TargetModel
{
    public string? Picture { get; set; }

    public decimal Balance { get; set; }

    public IDictionary<string, TargetModel> TargetArea { get; set; } = null!;
}
