using Microsoft.AspNetCore.Mvc;

namespace PhotonBypass.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BasicsController(ILogger<BasicsController> logger) : ControllerBase
{
    private readonly ILogger<BasicsController> logger = logger;


}
