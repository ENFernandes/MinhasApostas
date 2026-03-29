using Microsoft.AspNetCore.Mvc;

namespace SportsBetting.DataCollector.Api.Controllers;

/// <summary>
/// Health check endpoint for Docker and monitoring.
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Returns health status of the service.
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Service = "DataCollector.Api",
            Version = "1.0.0"
        });
    }
}
