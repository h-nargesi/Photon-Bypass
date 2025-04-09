using Microsoft.AspNetCore.Mvc;
using PhotonBypass.API.Models;

namespace PhotonBypass.API.Controllers;

[ApiController]
[Route("[controller]")]
public class AccountContoller(ILogger<AccountContoller> logger) : ControllerBase
{
    private readonly ILogger<AccountContoller> logger = logger;

    [HttpPost("change-ovpn")]
    public ActionResult ChangeOVPN(ChangeOvpnContext context)
    {
        return Ok();
    }
}
