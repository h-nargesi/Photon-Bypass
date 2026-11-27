using tik4net;

namespace PhotonBypass.ServerBridge.Tik4net;

/// <summary>
/// Creates a tik4net connection of a type. A delegate so the SSH/API adapters
/// and their tests choose the connection: the production registration calls
/// <see cref="ConnectionFactory.CreateConnection"/>, tests hand in a fake.
/// </summary>
public delegate ITikConnection TikConnectionFactory(TikConnectionType type);
