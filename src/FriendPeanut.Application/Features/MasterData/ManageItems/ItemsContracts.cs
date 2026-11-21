namespace FriendPeanut.Application.Features.MasterData.ManageItems;

public sealed record ItemDto(Guid Id, string Code, string NameAr, Guid CategoryId, Guid? BaseUnitId, string UnitStatus,
    bool LotTrackingEnabled, bool CanPurchase, bool CanSell, bool CanManufacture, bool IsActive, long Version);

public sealed record ItemCreateRequest(string Code, string NameAr, string NormalizedName, Guid CategoryId, Guid? BaseUnitId,
    bool CanPurchase, bool CanSell, bool CanManufacture);

public sealed record ItemUpdateRequest(string NameAr, string NormalizedName, Guid CategoryId, Guid? BaseUnitId,
    bool CanPurchase, bool CanSell, bool CanManufacture);