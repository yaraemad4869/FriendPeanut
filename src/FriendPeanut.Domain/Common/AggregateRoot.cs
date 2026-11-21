namespace FriendPeanut.Domain.Common;

public abstract class AggregateRoot<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;
    public long Version { get; protected set; } = 1;
    protected void BumpVersion() { Version++; }
}