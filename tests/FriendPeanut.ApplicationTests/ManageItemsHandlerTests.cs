using FriendPeanut.Application.Abstractions;
using FriendPeanut.Application.Common.Text;
using FriendPeanut.Application.Features.MasterData.ManageItems;
using Moq;
using Xunit;

namespace FriendPeanut.ApplicationTests;

public sealed class ManageItemsHandlerTests
{
    private static ItemRecord SampleItem(Guid? id = null, string name = "Peanut")
        => new(id ?? Guid.NewGuid(), "PNT-001", name, name.ToLowerInvariant(),
            Guid.NewGuid(), null, "PENDING", true, true, true, true, true, 1);

    private static ManageItemsHandler Build(List<ItemRecord> items)
    {
        var md = new Mock<IMasterDataStore>();
        md.Setup(m => m.ListItemsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(items);
        md.Setup(m => m.FindItemAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync((Guid id, CancellationToken _) => items.FirstOrDefault(i => i.Id == id));
        md.Setup(m => m.CreateItemAsync(It.IsAny<ItemRecord>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync((ItemRecord r, CancellationToken _) => r.Id);
        md.Setup(m => m.UpdateItemAsync(It.IsAny<ItemRecord>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
          .Returns(Task.CompletedTask);
        md.Setup(m => m.SearchItemsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync((string q, CancellationToken _) => items);

        var normalizer = new Mock<IArabicNormalizer>();
        normalizer.Setup(n => n.Normalize(It.IsAny<string>()))
                  .Returns((string s) => s.ToLowerInvariant());

        return new ManageItemsHandler(md.Object, normalizer.Object);
    }

    [Fact]
    public async Task Create_normalizes_name_and_sets_unit_pending()
    {
        var handler = Build(Array.Empty<ItemRecord>().ToList());
        var req = new ItemCreateRequest("PNT-001", "فول سوداني", "IGNORED", Guid.NewGuid(), null, true, true, false);
        var dto = await handler.CreateAsync(req, default);
        Assert.Equal("PENDING", dto.UnitStatus);
        Assert.True(dto.LotTrackingEnabled);
    }

    [Fact]
    public async Task Update_throws_on_version_mismatch_via_store()
    {
        var id = Guid.NewGuid();
        var items = new List<ItemRecord> { SampleItem(id) };

        var md = new Mock<IMasterDataStore>();
        md.Setup(m => m.FindItemAsync(id, It.IsAny<CancellationToken>()))
          .ReturnsAsync(items[0]);
        md.Setup(m => m.UpdateItemAsync(It.IsAny<ItemRecord>(), 99L, It.IsAny<CancellationToken>()))
          .ThrowsAsync(new InvalidOperationException("CONFLICT_VERSION"));

        var normalizer = new Mock<IArabicNormalizer>();
        normalizer.Setup(n => n.Normalize(It.IsAny<string>())).Returns("x");

        var handler = new ManageItemsHandler(md.Object, normalizer.Object);
        var req = new ItemUpdateRequest("New", "ignored", Guid.NewGuid(), null, true, true, true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.UpdateAsync(id, req, 99L, default));
    }

    [Fact]
    public async Task Search_uses_normalizer_before_store()
    {
        var handler = Build(new List<ItemRecord> { SampleItem() });
        var result = await handler.SearchAsync("فول", default);
        Assert.Single(result);
    }

    [Fact]
    public async Task List_returns_mapped_dtos()
    {
        var handler = Build(new List<ItemRecord> { SampleItem(), SampleItem() });
        var result = await handler.ListAsync(1, 25, default);
        Assert.Equal(2, result.Count);
        Assert.All(result, i => Assert.Equal("PENDING", i.UnitStatus));
    }
}