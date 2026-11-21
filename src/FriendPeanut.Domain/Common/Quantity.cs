namespace FriendPeanut.Domain.Common;

public sealed record Quantity
{
    public decimal Value { get; }
    public string UnitCode { get; }
    private Quantity(decimal value, string unitCode) { Value = value; UnitCode = unitCode; }

    public static Quantity Of(decimal value, string unitCode)
    {
        if (string.IsNullOrWhiteSpace(unitCode)) throw new DomainException("QUANTITY_UNIT_REQUIRED", "Unit code required.");
        if (decimal.Round(value, 3, MidpointRounding.ToEven) != value)
            throw new DomainException("QUANTITY_PRECISION", "Quantity must fit 3 decimal places.");
        return new Quantity(value, unitCode.ToUpperInvariant());
    }

    public Quantity Add(Quantity o)
    {
        if (o.UnitCode != UnitCode) throw new DomainException("QUANTITY_UNIT_MISMATCH", "Cannot combine units.");
        return new Quantity(Value + o.Value, UnitCode);
    }

    public Quantity Subtract(Quantity o)
    {
        if (o.UnitCode != UnitCode) throw new DomainException("QUANTITY_UNIT_MISMATCH", "Cannot combine units.");
        return new Quantity(Value - o.Value, UnitCode);
    }

    public override string ToString() => $"{Value:0.###} {UnitCode}";
}