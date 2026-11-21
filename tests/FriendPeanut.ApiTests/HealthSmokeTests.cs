using System.Net;
using Xunit;

namespace FriendPeanut.ApiTests;

public sealed class HealthSmokeTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    public HealthSmokeTests(TestWebApplicationFactory factory) { _factory = factory; }

    [Fact]
    public async Task Live_endpoint_returns_200()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/health/live");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_without_session_returns_401_or_redirect()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var resp = await client.GetAsync("/api/v1/me");
        Assert.True(resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect);
    }
}