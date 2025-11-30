using Refit;

namespace PhotonBypass.ServerBridge.Api;

public interface ILogin
{
    [Post("/login")]
    Task<string> Login(string username, string password);
}
