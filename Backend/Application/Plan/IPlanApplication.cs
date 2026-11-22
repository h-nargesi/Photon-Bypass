using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Plan;

public interface IPlanApplication
{
    /// <summary>Selectable realms for the purchase flow (Docs/22 D4).</summary>
    Task<ApiResult<List<RealmChoiceModel>>> GetRealms();

    /// <summary>
    /// Purchasable plans of the acting pricing group (Docs/24 §5) with axis
    /// pairs and realm choices. <paramref name="target"/> is the admin
    /// on-behalf user (D7 — their group applies); for a manager target the
    /// manager's own group stays in charge.
    /// </summary>
    Task<ApiResult<List<PlanCatalogModel>>> GetCatalog(string? target = null);

    /// <summary>D3 system suggestion for the acting user's next VPN username.</summary>
    Task<ApiResult<string>> SuggestUsername();

    Task<ApiResult<PlanStateModel>> GetPlanState(int account_id);

    Task<ApiResult<PlanInfoModel>> GetPlanInfo(int account_id);

    /// <summary>
    /// Price preview (Docs/24 §2): validates the choices against the plan,
    /// runs the calculator with the account's current state (D3) and composes
    /// the final four-axis limitation — no invoice, nothing persisted.
    /// </summary>
    Task<ApiResult<EstimateResult>> Estimate(int account_id, PurchaseRequest request);

    /// <summary>
    /// Renewal of an existing VPN account (Docs/22 D5): stacks on the same
    /// realm/username. Issues the invoice with the frozen calculator output
    /// (D9). <see cref="PurchaseRequest.RealmId"/> is mandatory when the
    /// account is stale (D6) and re-runs the realm migration.
    /// </summary>
    Task<ApiResult<RenewalResult>> Renewal(int account_id, PurchaseRequest request);

    /// <summary>
    /// First purchase (Docs/22 D9): issues the invoice whose settlement creates
    /// the VPN account and its first renewal in one transaction. No VPN account
    /// exists without a plan.
    /// </summary>
    Task<ApiResult<RenewalResult>> NewAccount(NewAccountRequest request);

    /// <summary>
    /// Settlement entry (wallet/receipt): parses the frozen invoice action —
    /// an <c>n|</c> prefix routes to the new-account branch, otherwise the
    /// renewal of the VPN username carried by the action. The calculator is
    /// never re-run (D9): the price and the limitations come from the action.
    /// </summary>
    Task<ApiResult<RenewalResult>> SettlePurchase(int payer_user_id, int? payment_id, string action, int price,
        int? invoice_code = null);
}
