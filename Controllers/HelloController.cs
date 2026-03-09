using Microsoft.AspNetCore.Mvc;

namespace scale_api_poc.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HelloController : ControllerBase
{
    /// <summary>
    /// Returns a greeting message.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok("Hello there");
    }
}
