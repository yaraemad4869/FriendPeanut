using FriendPeanut.Domain.Common;
using Xunit;

namespace FriendPeanut.DomainTests;

public sealed class QuantityTests
{
    [Fact] public void TON_to_KG_exact() { var t = Quantity.Of(2.5m, "TON"); Assert.Equal(2500m, t.Value * 1000m); }
    [Fact] public void Add_mismatched_unit_rejected() => Assert.Throws<DomainException>(() => Quantity.Of(1m,"KG").Add(Quantity.Of(1m,"TON")));
    [Fact] public void Rejects_4_decimals() => Assert.Throws<DomainException>(() => Quantity.Of(1.0001m, "KG"));
}