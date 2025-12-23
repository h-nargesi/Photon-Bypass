using Microsoft.AspNetCore.Mvc.Testing;

namespace PhotonBypass.Test.Initializer;

public abstract class ProgramLevelInitializer(WebApplicationFactory<PortalProgram> factory)
    : OutSourceLevelServiceInitializer, IClassFixture<WebApplicationFactory<PortalProgram>>
{
    protected readonly HttpClient Client = factory.CreateClient();
}