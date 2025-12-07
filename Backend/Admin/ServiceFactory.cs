namespace PhotonBypass.Admin;

public static class ServiceFactory
{
    public static TBuilder AddAdminServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        return builder;
    }
}
