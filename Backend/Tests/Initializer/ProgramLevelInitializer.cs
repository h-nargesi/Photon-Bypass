using Microsoft.AspNetCore.Mvc.Testing;

namespace PhotonBypass.Test;

public abstract class ProgramLevelInitializer : IClassFixture<WebApplicationFactory<PortalProgram>>
{
}
