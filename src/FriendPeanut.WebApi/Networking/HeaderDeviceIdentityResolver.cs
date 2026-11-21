using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.WebApi.Networking;

public sealed class HeaderDeviceIdentityResolver : IDeviceIdentityResolver
{
    public const string HeaderName = "X-Trusted-Device-Key";
    private readonly IHttpContextAccessor _http;
    public HeaderDeviceIdentityResolver(IHttpContextAccessor http) { _http = http; }

    public Task<string?> ResolveExternalKeyAsync(CancellationToken ct)
    {
        var ctx = _http.HttpContext;
        if (ctx is null) return Task.FromResult<string?>(null);
        if (!ctx.Request.Headers.TryGetValue(HeaderName, out var values)) return Task.FromResult<string?>(null);
        var key = values.ToString();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200) return Task.FromResult<string?>(null);
        return Task.FromResult<string?>(key);
    }
}