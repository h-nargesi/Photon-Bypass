using System.ComponentModel.DataAnnotations;

namespace PhotonBypass.Application.Management.Model;

public class ManagementOptions
{
    public string? DefaultCertPath { get; set; }

    public string? DefaultPrivateKeyOVpn { get; set; }

    public List<PaymentCard>? PaymentCards { get; set; }

    public int? WalletDeactivationThreshold { get; set; }

    public int ReceiptMaxBytes { get; set; } = 2_097_152;

    [Range(1, 10000)]
    public double UnmeteredPlanDemandGigabytes { get; set; } = 25;

    /// <summary>
    /// Minimum spacing between two x-ui UUID rotations per account (docs/16 D4):
    /// each updateClient can restart Xray on the fallback path. Default 60
    /// minutes; 0 disables the throttle.
    /// </summary>
    [Range(0, 525_600)]
    public int MinUuidRotationMinutes { get; set; } = 60;
}
