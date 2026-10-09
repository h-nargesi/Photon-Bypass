namespace PhotonBypass.Application.Management.Model;

public class ManagementOptions
{
    public string? DefaultCertPath { get; set; }

    public string? DefaultPrivateKeyOVpn { get; set; }

    public List<PaymentCard>? PaymentCards { get; set; }

    public int? WalletDeactivationThreshold { get; set; }

    public int ReceiptMaxBytes { get; set; } = 2_097_152;
}
