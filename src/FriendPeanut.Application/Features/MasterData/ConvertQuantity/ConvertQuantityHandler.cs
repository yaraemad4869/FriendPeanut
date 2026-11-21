using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.MasterData.ConvertQuantity;

public sealed record UnitDto(Guid Id, string Code, string NameAr, string Dimension, bool IsActive, long Version);
public sealed record UnitCreateRequest(string Code, string NameAr, string Dimension);
public sealed record UnitConversionDto(Guid Id, Guid ItemId, Guid FromUnitId, Guid ToUnitId, decimal Factor, DateOnly ValidFrom, DateOnly? ValidTo, long Version);
public sealed record UnitConversionCreateRequest(Guid ItemId, Guid FromUnitId, Guid ToUnitId, decimal Factor, DateOnly ValidFrom, DateOnly? ValidTo);

public sealed class ConvertQuantityHandler
{
    private readonly IMasterDataStore _md;
    public ConvertQuantityHandler(IMasterDataStore md) { _md = md; }

    public async Task<IReadOnlyList<UnitDto>> ListUnitsAsync(CancellationToken ct)
    {
        var rows = await _md.ListUnitsAsync(ct);
        return rows.Select(r => new UnitDto(r.Id, r.Code, r.NameAr, r.Dimension, r.IsActive, r.Version)).ToArray();
    }

    public async Task<UnitDto> CreateUnitAsync(UnitCreateRequest req, CancellationToken ct)
    {
        var rec = new UnitRecord(Guid.NewGuid(), req.Code, req.NameAr, req.Dimension, true, 1);
        var id = await _md.CreateUnitAsync(rec, ct);
        return new UnitDto(id, rec.Code, rec.NameAr, rec.Dimension, true, 1);
    }

    public async Task<IReadOnlyList<UnitConversionDto>> ListConversionsAsync(CancellationToken ct)
    {
        var rows = await _md.ListConversionsAsync(ct);
        return rows.Select(r => new UnitConversionDto(r.Id, r.ItemId, r.FromUnitId, r.ToUnitId, r.Factor, r.ValidFrom, r.ValidTo, r.Version)).ToArray();
    }

    public async Task<UnitConversionDto> CreateConversionAsync(UnitConversionCreateRequest req, CancellationToken ct)
    {
        if (req.Factor <= 0) throw new ArgumentOutOfRangeException(nameof(req), "Factor must be > 0.");
        if (req.FromUnitId == req.ToUnitId) throw new InvalidOperationException("From and To units must differ.");
        var rec = new UnitConversionRecord(Guid.NewGuid(), req.ItemId, req.FromUnitId, req.ToUnitId, req.Factor, req.ValidFrom, req.ValidTo, 1);
        var id = await _md.CreateConversionAsync(rec, ct);
        return new UnitConversionDto(id, rec.ItemId, rec.FromUnitId, rec.ToUnitId, rec.Factor, rec.ValidFrom, rec.ValidTo, 1);
    }
}