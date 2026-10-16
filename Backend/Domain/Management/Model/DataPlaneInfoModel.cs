namespace PhotonBypass.Domain.Management.Model;

public class DataPlaneInfoModel
{
    public string? Kind { get; init; }

    public bool SupportsOvpnCert { get; init; }

    public bool SupportsPasswordChange { get; init; }
}
