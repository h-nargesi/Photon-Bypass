using Refit;

namespace PhotonBypass.FreeRadius.WebService.ApiWrapper;

public interface PermanentUsers
{
    [Post("/enable-disable")]
    Task EnableDisable(object data);

    [Get("/view-password")]
    Task<string> ViewPassword([AliasAs("_dc")] long dc, string token, int user_id, string sel_language);
}