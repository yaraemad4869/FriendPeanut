using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.MasterData.ManageCostCenters;

public sealed record CostCenterDto(Guid Id, string Code, string NameAr, Guid? ParentId, bool IsActive, long Version);
public sealed record CostCenterCreateRequest(string Code, string NameAr, Guid? ParentId);
public sealed record CostCenterUpdateRequest(string NameAr, Guid? ParentId);

public sealed class ManageCostCentersHandler
{
    private readonly IMasterDataStore _md;
    public ManageCostCentersHandler(IMasterDataStore md) { _md = md; }

    public async Task<IReadOnlyList<CostCenterDto>> ListAsync(CancellationToken ct)
    {
        var rows = await _md.ListCostCentersAsync(ct);
        return rows.Select(r => new CostCenterDto(r.Id, r.Code, r.NameAr, r.ParentId, r.IsActive, r.Version)).ToArray();
    }

    public async Task<CostCenterDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var r = await _md.FindCostCenterAsync(id, ct);
        return r is null ? null : new CostCenterDto(r.Id, r.Code, r.NameAr, r.ParentId, r.IsActive, r.Version);
    }

    public async Task<CostCenterDto> CreateAsync(CostCenterCreateRequest req, CancellationToken ct)
    {
        var rec = new CostCenterRecord(Guid.NewGuid(), req.Code, req.NameAr, req.ParentId, true, 1);
        var id = await _md.CreateCostCenterAsync(rec, ct);
        return new CostCenterDto(id, rec.Code, rec.NameAr, rec.ParentId, true, 1);
    }

    public async Task<CostCenterDto?> UpdateAsync(Guid id, CostCenterUpdateRequest req, long expectedVersion, CancellationToken ct)
    {
        var existing = await _md.FindCostCenterAsync(id, ct);
        if (existing is null) return null;
        var updated = existing with { NameAr = req.NameAr, ParentId = req.ParentId };
        await _md.UpdateCostCenterAsync(updated, expectedVersion, ct);
        return new CostCenterDto(updated.Id, updated.Code, updated.NameAr, updated.ParentId, updated.IsActive, updated.Version);
    }
}