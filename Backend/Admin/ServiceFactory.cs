using Microsoft.IdentityModel.Tokens;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Application;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Tools;

namespace PhotonBypass.Admin;

public static class ServiceFactory
{
    public static TBuilder AddAdminServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        return builder;
    }
}
