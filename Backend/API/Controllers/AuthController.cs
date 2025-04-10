using Microsoft.AspNetCore.Mvc;

namespace PhotonBypass.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(ILogger<AuthController> logger) : ControllerBase
{
    private readonly ILogger<AuthController> logger = logger;


}
