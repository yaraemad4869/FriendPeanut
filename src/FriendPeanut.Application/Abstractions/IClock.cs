namespace FriendPeanut.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly TodayUtc { get; }
}