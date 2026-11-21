namespace FriendPeanut.Infrastructure.Persistence;

public sealed class DocumentRecord
{
    public Guid Id { get; private set; }

    public string Status { get; private set; } = "DRAFT";

    public long Version { get; private set; } = 1;

    private DocumentRecord()
    {
    }
}
