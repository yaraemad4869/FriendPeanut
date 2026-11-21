using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FriendPeanut.ApiTests;

public sealed class StockPostingAuthorizationTests
{
    [Fact]
    public async Task Stock_posting_without_authenticated_session_returns_401()
    {
        using var factory = new TestWebApplicationFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/stock-documents/{Guid.NewGuid()}/post");

        request.Headers.Add("Idempotency-Key", $"api-auth-{Guid.NewGuid():N}");
        request.Headers.Add("X-CSRF-Token", "csrf-test-token");
        request.Headers.Add("Cookie", "fp_csrf=csrf-test-token");

        request.Content = JsonContent.Create(new
        {
            ExpectedVersion = 1,
            NegativeStockOverrideRequested = false,
            NegativeStockOverrideReason = (string?)null
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}