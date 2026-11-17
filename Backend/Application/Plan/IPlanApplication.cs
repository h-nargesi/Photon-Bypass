using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Plan;

public interface IPlanApplication
{
    /// <summary>Selectable realms for the purchase flow (Docs/22 D4).</summary>
    Task<ApiResult<List<RealmChoiceModel>>> GetRealms();

    /// <summary>D3 system suggestion for the acting user's next VPN username.</summary>
    Task<ApiResult<string>> SuggestUsername();

    Task<ApiResult<PlanStateModel>> GetPlanState(int account_id);

    Task<ApiResult<PlanInfoModel>> GetPlanInfo(int account_id);

    Task<ApiResult<EstimateResult>> Estimate(int account_id, byte users, short days, int gigabytes);

    /// <summary>
    /// Renewal of an existing VPN account (Docs/22 D5): stacks on the same
    /// realm/username. <paramref name="realm_id"/> is mandatory when the
    /// account is stale (D6) and re-runs the realm migration.
    /// </summary>
    Task<ApiResult<RenewalResult>> Renewal(int account_id, byte users, short days, int gigabytes, int? realm_id);

    /// <summary>
    /// First purchase (Docs/22 D9): issues the invoice whose settlement creates
    /// the VPN account and its first renewal in one transaction. No VPN account
    /// exists without a plan. <paramref name="target"/> is the on-behalf owner
    /// (Docs/20) — null buys for the acting user; the wallet debit is always
    /// the paying actor.
    /// </summary>
    Task<ApiResult<RenewalResult>> NewAccount(string? username, int realm_id, byte users, short days, int gigabytes, string? target = null);

    /// <summary>
    /// Settlement entry (wallet/receipt): parses the invoice action — an
    /// <c>n|</c> prefix routes to the new-account branch, otherwise the renewal
    /// of the VPN username carried by the action.
    /// </summary>
    Task<ApiResult<RenewalResult>> SettlePurchase(int payer_user_id, int? payment_id, string action, int? invoice_code = null);
}
