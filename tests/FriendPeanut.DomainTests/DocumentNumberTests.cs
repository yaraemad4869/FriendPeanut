using FriendPeanut.Domain.Common;
using Xunit;

namespace FriendPeanut.DomainTests;

public sealed class DocumentNumberTests
{
    [Fact] public void Rejects_empty() => Assert.Throws<DomainException>(() => DocumentNumber.Parse(""));
    [Fact] public void Accepts_alnum_prefix() => Assert.Equal("STK-2026-0001", DocumentNumber.Parse("STK-2026-0001").Value);
    [Fact] public void Rejects_leading_symbol() => Assert.Throws<DomainException>(() => DocumentNumber.Parse("-ABC"));
}