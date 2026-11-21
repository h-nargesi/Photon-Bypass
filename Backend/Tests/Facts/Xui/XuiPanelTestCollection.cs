namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// Serializes every test class that talks to the Xui-Test VM: x-ui applies
/// client mutations through an unlocked read-modify-write of the inbound
/// settings, so concurrent panel writers can lose clients (Docs/23 s4.4).
/// Mock-tier Xui tests stay outside this collection and keep running in
/// parallel.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class XuiPanelTestCollection
{
    public const string Name = "Xui-Test panel";
}
