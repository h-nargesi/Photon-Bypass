namespace PhotonBypass.ServerBridge.Email;

/// <summary>
/// Conditional validation (an Enabled transport must be fully configured) that
/// data annotations cannot express; kept as a separate rule so tests can probe
/// it directly.
/// </summary>
internal static class EmailOptionsValidation
{
    public static bool IsValid(EmailOptions options)
    {
        return !options.Enabled ||
               (!string.IsNullOrWhiteSpace(options.Address) &&
                !string.IsNullOrWhiteSpace(options.Password) &&
                !string.IsNullOrWhiteSpace(options.Host));
    }
}
