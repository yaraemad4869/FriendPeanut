using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Infrastructure.Identity;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}