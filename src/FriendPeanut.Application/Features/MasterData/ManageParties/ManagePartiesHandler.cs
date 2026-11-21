using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.MasterData.ManageParties;

public sealed record PartyDto(Guid Id, string Code, string NameAr, string? Phone, string? Address, string? TaxNumber, bool IsActive, long Version);
public sealed record PartyCreateRequest(string Code, string NameAr, string? Phone, string? Address, string? TaxNumber);
public sealed record PartyUpdateRequest(string NameAr, string? Phone, string? Address, string? TaxNumber);

public sealed class ManagePartiesHandler
{
    private readonly IMasterDataStore _md;
    public ManagePartiesHandler(IMasterDataStore md) { _md = md; }

    public async Task<IReadOnlyList<PartyDto>> ListAsync(CancellationToken ct)
    {
        var rows = await _md.ListPartiesAsync(ct);
        return rows.Select(r => new PartyDto(r.Id, r.Code, r.NameAr, r.Phone, r.Address, r.TaxNumber, r.IsActive, r.Version)).ToArray();
    }

    public async Task<PartyDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var r = await _md.FindPartyAsync(id, ct);
        return r is null ? null : new PartyDto(r.Id, r.Code, r.NameAr, r.Phone, r.Address, r.TaxNumber, r.IsActive, r.Version);
    }

    public async Task<PartyDto> CreateAsync(PartyCreateRequest req, CancellationToken ct)
    {
        var rec = new PartyRecord(Guid.NewGuid(), req.Code, req.NameAr, req.Phone, req.Address, req.TaxNumber, true, 1);
        var id = await _md.CreatePartyAsync(rec, ct);
        return new PartyDto(id, rec.Code, rec.NameAr, rec.Phone, rec.Address, rec.TaxNumber, true, 1);
    }

    public async Task<PartyDto?> UpdateAsync(Guid id, PartyUpdateRequest req, long expectedVersion, CancellationToken ct)
    {
        var existing = await _md.FindPartyAsync(id, ct);
        if (existing is null) return null;
        var updated = existing with { NameAr = req.NameAr, Phone = req.Phone, Address = req.Address, TaxNumber = req.TaxNumber };
        await _md.UpdatePartyAsync(updated, expectedVersion, ct);
        return new PartyDto(updated.Id, updated.Code, updated.NameAr, updated.Phone, updated.Address, updated.TaxNumber, updated.IsActive, updated.Version);
    }
}