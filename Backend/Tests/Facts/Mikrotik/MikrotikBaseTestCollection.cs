namespace PhotonBypass.Test.Facts.Mikrotik;

/// <summary>
/// Serializes every test class that drives the Mikrotik-Base VM: its tests
/// restore the VM's snapshot and then share the router's users, certificates
/// and SSH service, so they must never run next to each other. The scenario
/// suite works on Mikrotik-Scenario and stays outside this collection.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class MikrotikBaseTestCollection
{
    public const string Name = "Mikrotik-Base router";
}
