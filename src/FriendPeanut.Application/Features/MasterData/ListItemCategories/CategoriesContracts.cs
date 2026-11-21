namespace FriendPeanut.Application.Features.MasterData.ListItemCategories;

public sealed record CategoryDto(Guid Id, string Code, string NameAr, Guid? ParentId, bool IsActive, long Version);
public sealed record CategoryCreateRequest(string Code, string NameAr, Guid? ParentId);
public sealed record CategoryUpdateRequest(string NameAr, Guid? ParentId);