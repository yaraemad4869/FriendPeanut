using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FriendPeanut.WebApi.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/health/live", async (HealthCheckService hc) =>
        {
            var r = await hc.CheckHealthAsync(x => x.Tags.Contains("live"));
            return r.Status == HealthStatus.Healthy ? Results.Ok(new { status = "healthy" })
                : Results.Problem(statusCode: 503, title: "UNHEALTHY");
        }).AllowAnonymous();

        app.MapGet("/api/v1/health/ready", async (HealthCheckService hc) =>
        {
            var r = await hc.CheckHealthAsync(x => x.Tags.Contains("ready"));
            return r.Status == HealthStatus.Healthy ? Results.Ok(new { status = "ready" })
                : Results.Problem(statusCode: 503, title: "NOT_READY");
        }).AllowAnonymous();

        return app;
    }
}