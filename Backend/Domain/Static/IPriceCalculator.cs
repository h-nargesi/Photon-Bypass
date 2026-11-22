namespace PhotonBypass.Domain.Static;

public interface IPriceCalculator
{
    /// <summary>
    /// Runs the compiled calculator of <see cref="PriceCalculationInput.PlanId"/>
    /// (Docs/24 D2). First use lazily compiles every active plan; a failing or
    /// missing calculator for the plan id throws.
    /// </summary>
    Task<PriceCalculationOutput> Calculate(PriceCalculationInput input);

    /// <summary>
    /// D4: forced pool rebuild for out-of-band edits (direct SQL raises no
    /// entity event) — the admin reload endpoint calls this.
    /// </summary>
    Task Reload();

    /// <summary>Compiles a candidate snippet without touching the pool (admin save-time check).</summary>
    void ValidateCode(string code);
}
