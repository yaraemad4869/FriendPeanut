using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly FriendPeanutDbContext _db;
    public UnitOfWork(FriendPeanutDbContext db) { _db = db; }
    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
    public Task BeginAsync(CancellationToken ct) => _db.Database.BeginTransactionAsync(ct);
    public Task CommitAsync(CancellationToken ct) => _db.Database.CommitTransactionAsync(ct);
    public Task RollbackAsync(CancellationToken ct) => _db.Database.RollbackTransactionAsync(ct);
}