using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.MasterData.SearchItemAliases;

public sealed record AddAliasRequest(string AliasAr, string NormalizedAlias);

public sealed class SearchItemAliasesHandler
{
    private readonly IMasterDataStore _md;
    public SearchItemAliasesHandler(IMasterDataStore md) { _md = md; }
    public Task<IReadOnlyList<ItemAliasRecord>> ListForItemAsync(Guid itemId, CancellationToken ct) => _md.ListItemAliasesAsync(itemId, ct);
    public Task AddAsync(Guid itemId, AddAliasRequest req, CancellationToken ct) => _md.AddItemAliasAsync(itemId, req.AliasAr, req.NormalizedAlias, ct);
}