using FriendPeanut.Domain.Common;
using Xunit;

namespace FriendPeanut.DomainTests;

public sealed class MoneyTests
{
    [Fact] public void Rejects_more_than_two_decimals() => Assert.Throws<DomainException>(() => Money.Of(1.001m, "EGP"));
    [Fact] public void Add_requires_same_currency() { var a = Money.Of(10m, "EGP"); Assert.Throws<DomainException>(() => a.Add(Money.Of(5m, "USD"))); }
    [Fact] public void Add_same_currency_works() { var s = Money.Of(10m, "EGP").Add(Money.Of(5m, "EGP")); Assert.Equal(15m, s.Amount); Assert.Equal("EGP", s.CurrencyCode); }
}