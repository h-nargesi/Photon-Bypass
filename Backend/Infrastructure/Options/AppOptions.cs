using System.ComponentModel.DataAnnotations;

namespace PhotonBypass.Infra.Options;

public class AppOptions : IOptionsRoot
{
    [Required(AllowEmptyStrings = false)]
    public string Name { get; set; } = "PhotonBypass.WebAPI";
}
