namespace FriendPeanut.Application.Abstractions;

public sealed record UserScopeRecord(Guid Id, Guid UserId, string ScopeKind, Guid? WarehouseId, Guid? FactoryId, string? DepartmentCode);

public interface IScopeStore
{
    Task<IReadOnlyList<UserScopeRecord>> ListForUserAsync(Guid userId, CancellationToken ct);
    Task ReplaceForUserAsync(Guid userId, IReadOnlyList<UserScopeRecord> scopes, CancellationToken ct);
}