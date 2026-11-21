namespace FriendPeanut.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Username { get; }
    bool IsAuthenticated { get; }
    IReadOnlySet<string> Permissions { get; }
    IReadOnlySet<Guid> WarehouseScopes { get; }
    IReadOnlySet<Guid> FactoryScopes { get; }
    IReadOnlySet<string> DepartmentScopes { get; }
    bool HasRecentMfa { get; }
    bool HasPermission(string code);
    bool AllowsWarehouse(Guid warehouseId);
    bool AllowsFactory(Guid factoryId);
    bool AllowsDepartment(string code);
}