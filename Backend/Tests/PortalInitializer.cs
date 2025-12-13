using Microsoft.AspNetCore.Mvc.Testing;

namespace PhotonBypass.Test;

public abstract class PortalInitializer : IClassFixture<WebApplicationFactory<PortalProgram>>
{
}
