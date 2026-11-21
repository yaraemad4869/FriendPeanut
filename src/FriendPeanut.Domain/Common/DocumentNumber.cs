namespace FriendPeanut.Domain.Common;

public sealed record DocumentNumber
{
    public string Value { get; }
    private DocumentNumber(string value) { Value = value; }
    public static DocumentNumber Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 60) throw new DomainException("DOCUMENT_NUMBER_INVALID","Document number must be non-empty and <= 60 chars.");
        if (!char.IsLetterOrDigit(value[0])) throw new DomainException("DOCUMENT_NUMBER_INVALID","Document number must start with a letter or digit.");
        return new DocumentNumber(value);
    }
    public override string ToString() => Value;
}