using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Domain.Static;

public class PriceEntity : IBaseEntity
{
    public int Id { get; set; }

    public bool IsActive { get; set; }

    public PlanType PlanType { get; set; }

    public string Title { get; set; } = null!;

    public string Caption { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string CalculatorCode { get; set; } = null!;
}
