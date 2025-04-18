using PhotonBypass.Domain;

namespace PhotonBypass.API.Basical;

static class ServiceInjection
{
    public static void AddJobContextService(this IServiceCollection services)
    {
        services.AddScoped<IJobContext, JobContext>();
    }
}
