namespace FriendPeanut.Domain.Common;

public sealed record Money
{
    public decimal Amount { get; }
    public string CurrencyCode { get; }
    private Money(decimal amount, string currencyCode) { Amount = amount; CurrencyCode = currencyCode; }

    public static Money Of(decimal amount, string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
            throw new DomainException("MONEY_CURRENCY_INVALID", "Currency code must be ISO-4217 three letters.");
        if (decimal.Round(amount, 2, MidpointRounding.ToEven) != amount)
            throw new DomainException("MONEY_PRECISION", "Money must be representable in 2 decimal places.");
        return new Money(amount, currencyCode.ToUpperInvariant());
    }

    public Money Add(Money o)
    {
        if (o.CurrencyCode != CurrencyCode) throw new DomainException("MONEY_CURRENCY_MISMATCH", "Cannot combine different currencies.");
        return new Money(Amount + o.Amount, CurrencyCode);
    }

    public Money Subtract(Money o)
    {
        if (o.CurrencyCode != CurrencyCode) throw new DomainException("MONEY_CURRENCY_MISMATCH", "Cannot combine different currencies.");
        return new Money(Amount - o.Amount, CurrencyCode);
    }

    public override string ToString() => $"{Amount:0.00} {CurrencyCode}";
}