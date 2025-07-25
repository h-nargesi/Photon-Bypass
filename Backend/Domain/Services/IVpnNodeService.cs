﻿using PhotonBypass.Domain.Radius;

namespace PhotonBypass.Domain.Services;

public interface IVpnNodeService
{
    Task<(NasEntity server, IList<UserConnectionBinding> connections)> GetActiveConnections(NasEntity server, string username);

    Task<bool> CloseConnection(NasEntity server, string sessionId);

    Task<bool> CloseConnections(IEnumerable<NasEntity> servers, string username, int count);

    Task GetCertificate(NasEntity server, string username, CertContext default_context);
}
