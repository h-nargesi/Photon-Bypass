namespace PhotonBypass.Domain.Account.Model;

public enum BalanceStatus : byte
{
    Pending = 0,
    Completed,
    Failed,
    Canceled,
}