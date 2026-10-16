namespace PhotonBypass.Domain.Management.Model;

public class DataPlaneInfoModel
{
    public string? Kind { get; init; }

    public bool SupportsOvpnCert { get; init; }

    public bool SupportsPasswordChange { get; init; }

    /// <summary>
    /// True for x-ui data-planes, where the credential-change path is an
    /// automatic UUID rotation (docs/16 D4) instead of a password change.
    /// </summary>
    public bool SupportsCredentialRotation { get; init; }
}
