using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using PhotonBypass.Test.Initializer.OutSourceManager;
using static PhotonBypass.Test.Initializer.ProgramLevelInitializer;

namespace PhotonBypass.Test.Initializer;

public abstract class ProgramLevelInitializer(Factory factory) : IClassFixture<Factory>
{
    private const string TestPackageMikrotik = "Mikrotik-Scenario";
    private const string TestPackageDatabase = "DbScenario";

    protected readonly HttpClient Client = factory.CreateClient();

    public class Factory : WebApplicationFactory<PortalProgram>
    {
        public IHost? App { get; private set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                ServiceInitializer.AddDefaultServices(services, [typeof(IOutSourceLevelService), typeof(IOptionsMoq)]);

                var sp = services.BuildServiceProvider();

                using var scope = sp.CreateScope();

                var init_mikrotik = Task.CompletedTask; //scope.Register<MikrotikInitializer>(TestPackageMikrotik, this);
                var init_database = scope.Register<LocalDatabaseInitializer>(TestPackageDatabase, this);

                Task.WaitAll(init_mikrotik, init_database);
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            return App = base.CreateHost(builder);
        }
    }
}