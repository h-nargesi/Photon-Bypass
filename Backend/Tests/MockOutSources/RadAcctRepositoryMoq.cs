using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockOutSources;

public class RadAcctRepositoryMoq : Mock<IRadAcctRepository>, IOutSourceMoq
{
    public RadAcctRepositoryMoq() : this(FilePath)
    {
    }

    protected RadAcctRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path);
        var data = JsonSerializer.Deserialize<List<RadAcctEntity>>(raw_text)
                       ?.GroupBy(k => k.Username)
                       .ToDictionary(k => k.Key, v => v.ToList())
                   ?? new Dictionary<string, List<RadAcctEntity>>();

        Setup(x => x.GetCurrentConnectionList(It.IsAny<string>()))
            .Returns<string>(username =>
            {
                if (!data.TryGetValue(username, out var result))
                {
                    result = [];
                }

                return Task.FromResult<IList<RadAcctEntity>>(result);
            });
    }

    private const string FilePath = "Data/rad-account.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<RadAcctRepositoryMoq>();
        services.AddLazyScoped(s => s.GetRequiredService<RadAcctRepositoryMoq>().Object);
    }
}