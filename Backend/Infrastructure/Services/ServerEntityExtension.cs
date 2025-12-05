using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Infra.Services;

internal static class ServerEntityExtension
{
    public static async Task<List<TResult>> RunJob<TResult>(this IEnumerable<ServerEntity> radius_list,
        Func<ServerEntity, Task<TResult>> function)
    {
        var tasks = radius_list.Select(function).ToArray();

        await Task.WhenAll(tasks);

        return tasks.Select(t => t.Result).ToList();
    }
    
    public static async Task RunJob(this IEnumerable<ServerEntity> radius_list,
        Func<ServerEntity, Task> function)
    {
        var tasks = radius_list.Select(function).ToArray();

        await Task.WhenAll(tasks);
    }
}