using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.MasterData.ListItemCategories;

public sealed class ListItemCategoriesHandler
{
    private readonly IMasterDataStore _md;
    public ListItemCategoriesHandler(IMasterDataStore md) { _md = md; }

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken ct)
    {
        var rows = await _md.ListCategoriesAsync(ct);
        return rows.Select(r => new CategoryDto(r.Id, r.Code, r.NameAr, r.ParentId, r.IsActive, r.Version)).ToArray();
    }

    public async Task<CategoryDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var r = await _md.FindCategoryAsync(id, ct);
        return r is null ? null : new CategoryDto(r.Id, r.Code, r.NameAr, r.ParentId, r.IsActive, r.Version);
    }

    public async Task<CategoryDto> CreateAsync(CategoryCreateRequest req, CancellationToken ct)
    {
        var rec = new CategoryRecord(Guid.NewGuid(), req.Code, req.NameAr, req.ParentId, true, 1);
        var id = await _md.CreateCategoryAsync(rec, ct);
        return new CategoryDto(id, rec.Code, rec.NameAr, rec.ParentId, true, 1);
    }

    public async Task<CategoryDto?> UpdateAsync(Guid id, CategoryUpdateRequest req, long expectedVersion, CancellationToken ct)
    {
        var existing = await _md.FindCategoryAsync(id, ct);
        if (existing is null) return null;
        var updated = existing with { NameAr = req.NameAr, ParentId = req.ParentId };
        await _md.UpdateCategoryAsync(updated, expectedVersion, ct);
        return new CategoryDto(updated.Id, updated.Code, updated.NameAr, updated.ParentId, updated.IsActive, updated.Version);
    }
}