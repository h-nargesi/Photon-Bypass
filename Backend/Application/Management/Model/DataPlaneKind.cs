namespace PhotonBypass.Application.Management.Model;

/// <summary>
/// Data-plane kind identifiers reported to the frontend.
/// Mirrors the single-bit feature matching of the infrastructure sync dispatcher
/// without taking a dependency on the Infrastructure layer.
/// </summary>
public static class DataPlaneKind
{
    public const string UserManager = "usermanager";

    public const string RadiusDesk = "radiusdesk";

    public const string Xui = "xui";
}
