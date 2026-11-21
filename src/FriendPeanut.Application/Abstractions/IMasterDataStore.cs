namespace FriendPeanut.Application.Abstractions;

public sealed record ItemRecord(Guid Id, string Code, string NameAr, string NormalizedName, Guid CategoryId,
    Guid? BaseUnitId, string UnitStatus, bool LotTrackingEnabled,
    bool CanPurchase, bool CanSell, bool CanManufacture, bool IsActive, long Version);

public sealed record CategoryRecord(Guid Id, string Code, string NameAr, Guid? ParentId, bool IsActive, long Version);
public sealed record UnitRecord(Guid Id, string Code, string NameAr, string Dimension, bool IsActive, long Version);
public sealed record UnitConversionRecord(Guid Id, Guid ItemId, Guid FromUnitId, Guid ToUnitId, decimal Factor, DateOnly ValidFrom, DateOnly? ValidTo, long Version);
public sealed record LotRecord(Guid Id, Guid ItemId, string Code, bool IsSystemUntracked, DateOnly? ReceivedDate, long Version);
public sealed record WarehouseRecord(Guid Id, string Code, string NameAr, Guid? SiteId, Guid? FactoryId, long Version);
public sealed record PartyRecord(Guid Id, string Code, string NameAr, string? Phone, string? Address, string? TaxNumber, bool IsActive, long Version);
public sealed record CostCenterRecord(Guid Id, string Code, string NameAr, Guid? ParentId, bool IsActive, long Version);
public sealed record ItemAliasRecord(Guid Id, Guid ItemId, string AliasAr, string NormalizedAlias);

public interface IMasterDataStore
{
    Task<IReadOnlyList<ItemRecord>> ListItemsAsync(int page, int pageSize, CancellationToken ct);
    Task<ItemRecord?> FindItemAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateItemAsync(ItemRecord item, CancellationToken ct);
    Task UpdateItemAsync(ItemRecord item, long expectedVersion, CancellationToken ct);
    Task DeactivateItemAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ItemRecord>> SearchItemsAsync(string query, CancellationToken ct);

    Task<IReadOnlyList<CategoryRecord>> ListCategoriesAsync(CancellationToken ct);
    Task<CategoryRecord?> FindCategoryAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateCategoryAsync(CategoryRecord c, CancellationToken ct);
    Task UpdateCategoryAsync(CategoryRecord c, long expectedVersion, CancellationToken ct);

    Task<IReadOnlyList<UnitRecord>> ListUnitsAsync(CancellationToken ct);
    Task<UnitRecord?> FindUnitAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateUnitAsync(UnitRecord u, CancellationToken ct);
    Task UpdateUnitAsync(UnitRecord u, long expectedVersion, CancellationToken ct);

    Task<IReadOnlyList<UnitConversionRecord>> ListConversionsAsync(CancellationToken ct);
    Task<UnitConversionRecord?> FindConversionAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateConversionAsync(UnitConversionRecord c, CancellationToken ct);
    Task UpdateConversionAsync(UnitConversionRecord c, long expectedVersion, CancellationToken ct);

    Task<IReadOnlyList<WarehouseRecord>> ListWarehousesAsync(CancellationToken ct);
    Task<WarehouseRecord?> FindWarehouseAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<PartyRecord>> ListPartiesAsync(CancellationToken ct);
    Task<PartyRecord?> FindPartyAsync(Guid id, CancellationToken ct);
    Task<Guid> CreatePartyAsync(PartyRecord p, CancellationToken ct);
    Task UpdatePartyAsync(PartyRecord p, long expectedVersion, CancellationToken ct);

    Task<IReadOnlyList<CostCenterRecord>> ListCostCentersAsync(CancellationToken ct);
    Task<CostCenterRecord?> FindCostCenterAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateCostCenterAsync(CostCenterRecord c, CancellationToken ct);
    Task UpdateCostCenterAsync(CostCenterRecord c, long expectedVersion, CancellationToken ct);

    Task<IReadOnlyList<LotRecord>> ListLotsAsync(CancellationToken ct);
    Task<LotRecord?> FindLotAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateLotAsync(LotRecord l, CancellationToken ct);
    Task UpdateLotAsync(LotRecord l, long expectedVersion, CancellationToken ct);

    Task AddItemAliasAsync(Guid itemId, string aliasAr, string normalizedAlias, CancellationToken ct);
    Task<IReadOnlyList<ItemAliasRecord>> ListItemAliasesAsync(Guid itemId, CancellationToken ct);
}