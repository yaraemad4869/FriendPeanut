using FriendPeanut.Application.Abstractions;
using FriendPeanut.Application.Common.Text;

namespace FriendPeanut.Application.Features.MasterData.ManageItems;

public sealed class ManageItemsHandler
{
    private readonly IMasterDataStore _md;
    private readonly IArabicNormalizer _normalizer;

    public ManageItemsHandler(IMasterDataStore md, IArabicNormalizer normalizer)
    { _md = md; _normalizer = normalizer; }

    public async Task<IReadOnlyList<ItemDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var rows = await _md.ListItemsAsync(page, pageSize, ct);
        return rows.Select(Map).ToArray();
    }

    public async Task<ItemDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var r = await _md.FindItemAsync(id, ct);
        return r is null ? null : Map(r);
    }

    public async Task<ItemDto> CreateAsync(ItemCreateRequest req, CancellationToken ct)
    {
        var normalized = _normalizer.Normalize(req.NameAr);
        var rec = new ItemRecord(Guid.NewGuid(), req.Code, req.NameAr, normalized, req.CategoryId,
            req.BaseUnitId, "PENDING", true, req.CanPurchase, req.CanSell, req.CanManufacture, true, 1);
        var id = await _md.CreateItemAsync(rec, ct);
        return Map(rec with { Id = id });
    }

    public async Task<ItemDto?> UpdateAsync(Guid id, ItemUpdateRequest req, long expectedVersion, CancellationToken ct)
    {
        var existing = await _md.FindItemAsync(id, ct);
        if (existing is null) return null;
        var updated = existing with
        {
            NameAr = req.NameAr,
            NormalizedName = _normalizer.Normalize(req.NameAr),
            CategoryId = req.CategoryId,
            BaseUnitId = req.BaseUnitId,
            CanPurchase = req.CanPurchase,
            CanSell = req.CanSell,
            CanManufacture = req.CanManufacture,
            UnitStatus = req.BaseUnitId is null ? existing.UnitStatus : "APPROVED"
        };
        await _md.UpdateItemAsync(updated, expectedVersion, ct);
        return Map(updated);
    }

    public Task DeactivateAsync(Guid id, CancellationToken ct) => _md.DeactivateItemAsync(id, ct);

    public async Task<IReadOnlyList<ItemDto>> SearchAsync(string q, CancellationToken ct)
    {
        var normalized = _normalizer.Normalize(q ?? string.Empty);
        var rows = await _md.SearchItemsAsync(normalized, ct);
        return rows.Select(Map).ToArray();
    }

    private static ItemDto Map(ItemRecord r) =>
        new(r.Id, r.Code, r.NameAr, r.CategoryId, r.BaseUnitId, r.UnitStatus,
            r.LotTrackingEnabled, r.CanPurchase, r.CanSell, r.CanManufacture, r.IsActive, r.Version);
}
