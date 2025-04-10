namespace PhotonBypass.Domain.Model;

public class PriceModel
{
    public string Title { get; set; } = string.Empty;

    public string Caption { get; set; } = string.Empty;

    public IEnumerable<string> Description { get; set; } = [];
}
