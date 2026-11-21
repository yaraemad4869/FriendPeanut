namespace FriendPeanut.Application.Abstractions;

public sealed record IdempotencyReservation(Guid Id, Guid UserId, string RouteKey, string Key, string RequestHash, string State, int? ResponseStatus, string? ResponseBody);

public interface IIdempotencyStore
{
    Task<(IdempotencyReservation? completed, bool conflict)> ReserveOrReplayAsync(
        Guid userId, string routeKey, string key, string requestHash, TimeSpan retention, CancellationToken ct);
    Task SaveCompletedAsync(Guid reservationId, int status, string body, CancellationToken ct);
}