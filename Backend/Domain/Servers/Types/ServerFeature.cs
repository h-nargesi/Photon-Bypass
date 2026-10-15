namespace PhotonBypass.Domain.Servers.Types;

[Flags]
public enum ServerFeature
{
    Radius = 0xFF,
    RadiusDesk = 0x1,
    UserManager = 0x2,
    Xui = 0x4,

    Nas = 0xFF00,
    Ikev2 = 0x100,
    Sstp = 0x200,
    Ovpn = 0x400,
    V2ray = 0x800,
}