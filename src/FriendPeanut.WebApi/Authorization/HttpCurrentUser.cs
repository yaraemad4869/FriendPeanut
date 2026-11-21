using System.Security.Claims;
using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.WebApi.Authorization;

public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;
    public HttpCurrentUser(IHttpContextAccessor http) { _http = http; }
    private ClaimsPrincipal? P => _http.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var raw = P?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var g) ? g : null;
        }
    }
    public string? Username => P?.FindFirstValue(ClaimTypes.Name);
    public bool IsAuthenticated => P?.Identity?.IsAuthenticated == true;

    public IReadOnlySet<string> Permissions => P?.FindAll("perm").Select(c => c.Value).ToHashSet() ?? new HashSet<string>();
    public IReadOnlySet<Guid> WarehouseScopes => P?.FindAll("scope.wh").Select(c => Guid.Parse(c.Value)).ToHashSet() ?? new HashSet<Guid>();
    public IReadOnlySet<Guid> FactoryScopes => P?.FindAll("scope.fc").Select(c => Guid.Parse(c.Value)).ToHashSet() ?? new HashSet<Guid>();
    public IReadOnlySet<string> DepartmentScopes => P?.FindAll("scope.dp").Select(c => c.Value).ToHashSet() ?? new HashSet<string>();

    public bool HasRecentMfa => P?.FindFirstValue("mfa_at") is string s && DateTimeOffset.TryParse(s, out var t) && t > DateTimeOffset.UtcNow.AddMinutes(-5);

    public bool HasPermission(string code) => Permissions.Contains(code);
    public bool AllowsWarehouse(Guid warehouseId) => WarehouseScopes.Contains(warehouseId);
    public bool AllowsFactory(Guid factoryId) => FactoryScopes.Contains(factoryId);
    public bool AllowsDepartment(string code) => DepartmentScopes.Contains(code);
}