using Microsoft.AspNetCore.Mvc;

namespace SmartFileImport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "Smart File Import API"
        });
    }
}
