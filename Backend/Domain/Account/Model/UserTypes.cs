namespace PhotonBypass.Domain.Account.Model;

[Flags]
public enum UserTypes : byte
{
    None = 0,
    OldUser = 1,

    // AllowMonthly (2) is gone (Docs/24 §2): group plans define every
    // purchasable combination, so the monthly gate is redundant.
    AllowSelfService = 4,
}