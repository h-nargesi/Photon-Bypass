using System.Text;
using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Tools;
using System.Text.RegularExpressions;
using PhotonBypass.Domain.Servers;

namespace PhotonBypass.Test.Application;

public class ServerManagementServiceTest : ServiceInitializer
{
    [Fact]
    public async Task GetAvailableRealm_Check()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        var realm = await manager.GetAvailableRealm();

        Assert.NotNull(realm);
        Assert.Equal(4, realm.Id);
    }

    [Fact]
    public async Task GetDefaultCertificate_MultiRealm()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var server_repo = scope.ServiceProvider.GetRequiredService<IServerRepository>();

        var context = await manager.GetDefaultCertificate(null);
        var nas_domain_list = await server_repo.GetAllActiveNasDomainInRealm(null);
        var remotes = string.Join("\n", nas_domain_list.Select(domain => $"remote {domain}"));
        
        Assert.NotNull(context);
        
        var text_file = Encoding.UTF8.GetString(context.CertFile);

        Assert.Contains("setenv FRIENDLY_NAME \"All\"", text_file);
        Assert.Contains(remotes, text_file);
    }
    
    [Fact]
    public async Task GetDefaultCertificate_SingleRealm()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var server_repo = scope.ServiceProvider.GetRequiredService<IServerRepository>();
        var realm_repo = scope.ServiceProvider.GetRequiredService<IRealmRepository>();

        var realm_name = await realm_repo.GetName(1);
        var context = await manager.GetDefaultCertificate(1);
        var nas_domain_list = await server_repo.GetAllActiveNasDomainInRealm(1);
        var remotes = string.Join("\n", nas_domain_list.Select(domain => $"remote {domain}"));
        
        Assert.NotNull(context);
        
        var text_file = Encoding.UTF8.GetString(context.CertFile);

        Assert.Contains($"setenv FRIENDLY_NAME \"{realm_name}\"", text_file);
        Assert.Contains(remotes, text_file);
    }

    [Fact(Skip = "Not implemented")]
    public async Task CheckUserServerBalance_Check()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        var is_called = false;
        // OnSocialMediaCall += (_, alarms) =>
        // {
        //     is_called = true;
        //
        //     foreach (var alarm in alarms)
        //     {
        //         var m = PercentCheck().Match(alarm);
        //
        //         Assert.NotNull(m);
        //
        //         Assert.True(double.TryParse(m.Groups[1].Value, out var percent));
        //         Assert.True(int.TryParse(m.Groups[2].Value, out var capacity));
        //
        //         switch (capacity)
        //         {
        //             case 100:
        //                 Assert.Equal(8, percent);
        //                 break;
        //             case 300:
        //                 Assert.Equal(95, percent);
        //                 break;
        //             default:
        //                 Assert.True(false);
        //                 break;
        //         }
        //     }
        // };

        await manager.CheckUserServerBalance();

        Assert.True(is_called);
    }
}