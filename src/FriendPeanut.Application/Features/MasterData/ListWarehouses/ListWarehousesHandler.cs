using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.MasterData.ListWarehouses;

public sealed record WarehouseDto(Guid Id, string Code, string NameAr, Guid? SiteId, Guid? FactoryId);

public sealed class ListWarehousesHandler
{
    private readonly IMasterDataStore _md;
    public ListWarehousesHandler(IMasterDataStore md) { _md = md; }

    public async Task<IReadOnlyList<WarehouseDto>> HandleAsync(CancellationToken ct)
    {
        var rows = await _md.ListWarehousesAsync(ct);
        return rows.Select(r => new WarehouseDto(r.Id, r.Code, r.NameAr, r.SiteId, r.FactoryId)).ToArray();
    }
}