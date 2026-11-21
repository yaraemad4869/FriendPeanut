using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.MasterData.ManageLots;

public sealed record LotDto(Guid Id, Guid ItemId, string Code, bool IsSystemUntracked, DateOnly? ReceivedDate, long Version);
public sealed record LotCreateRequest(Guid ItemId, string Code, DateOnly? ReceivedDate);

public sealed class ManageLotsHandler
{
    private readonly IMasterDataStore _md;
    public ManageLotsHandler(IMasterDataStore md) { _md = md; }

    public async Task<IReadOnlyList<LotDto>> ListAsync(CancellationToken ct)
    {
        var rows = await _md.ListLotsAsync(ct);
        return rows.Select(r => new LotDto(r.Id, r.ItemId, r.Code, r.IsSystemUntracked, r.ReceivedDate, r.Version)).ToArray();
    }

    public async Task<LotDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var r = await _md.FindLotAsync(id, ct);
        return r is null ? null : new LotDto(r.Id, r.ItemId, r.Code, r.IsSystemUntracked, r.ReceivedDate, r.Version);
    }

    public async Task<LotDto> CreateAsync(LotCreateRequest req, CancellationToken ct)
    {
        var rec = new LotRecord(Guid.NewGuid(), req.ItemId, req.Code, false, req.ReceivedDate, 1);
        var id = await _md.CreateLotAsync(rec, ct);
        return new LotDto(id, rec.ItemId, rec.Code, rec.IsSystemUntracked, rec.ReceivedDate, 1);
    }
}