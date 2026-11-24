using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Infrastructure.Persistence;

namespace MTL.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(AppDbContext db) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var databaseReady = await db.Database.CanConnectAsync(cancellationToken);
        return databaseReady
            ? Ok(new { status = "healthy", database = "ready", timestamp = DateTimeOffset.UtcNow })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unhealthy",
                database = "unavailable",
                timestamp = DateTimeOffset.UtcNow
            });
    }
}

