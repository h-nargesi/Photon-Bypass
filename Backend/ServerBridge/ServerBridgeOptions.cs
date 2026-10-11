namespace PhotonBypass.ServerBridge;

public class ServerBridgeOptions
{
    public int SshConnectTimeoutSeconds { get; set; } = 15;

    public int SshKeepAliveIntervalSeconds { get; set; } = 30;

    public int SshCommandTimeoutSeconds { get; set; } = 30;

    public int Tik4NetOpenTimeoutSeconds { get; set; } = 15;
}
