namespace PhotonBypass.Domain.Account.Model;

[Flags]
public enum UserTypes : byte
{
    None = 0,
    OldUser = 1,
    AllowMonthly = 2,
}