namespace PhotonBypass.Application.Basics.Model;

public class PriceModel
{
    public string Title { get; set; } = null!;

    public string Caption { get; set; } = null!;

    public IEnumerable<string> Description { get; set; } = null!;
}
