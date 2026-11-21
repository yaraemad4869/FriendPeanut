using System.Text.Json;
using FriendPeanut.Application.Abstractions;
using FriendPeanut.Application.Features.Inventory.PostStockDocument;
using FriendPeanut.Domain.Inventory;
using Xunit;

namespace FriendPeanut.AcceptanceTests;

public sealed class StockPostingServiceTests
{
    private static readonly Guid ActorId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static readonly Guid DocumentId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");

    private static readonly Guid ItemId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");

    private static readonly Guid WarehouseId =
        Guid.Parse("40000000-0000-0000-0000-000000000001");

    private static readonly Guid LotId =
        Guid.Parse("50000000-0000-0000-0000-000000000001");

    private static readonly Guid UnitId =
        Guid.Parse("60000000-0000-0000-0000-000000000001");

    private static readonly Guid LineId =
        Guid.Parse("70000000-0000-0000-0000-000000000001");

    private static StockMovement Inbound(decimal quantity = 10m) =>
        new(
            LineId,
            ItemId,
            WarehouseId,
            LotId,
            UnitId,
            quantity,
            StockMovementDirection.Inbound);

    private static StockMovement Outbound(decimal quantity = 10m) =>
        new(
            LineId,
            ItemId,
            WarehouseId,
            LotId,
            UnitId,
            quantity,
            StockMovementDirection.Outbound);

    private static PostStockDocumentCommand Command(
        string key = "acceptance-key-001",
        bool overrideNegative = false,
        string? reason = null,
        long expectedVersion = 1) =>
        new(
            DocumentId,
            expectedVersion,
            key,
            overrideNegative,
            reason);

    private static TestContext CreateContext(
        IReadOnlyList<StockMovement>? movements = null,
        decimal availableQuantity = 100m)
    {
        var repository = new FakeStockPostingRepository
        {
            Movements = movements ?? new[] { Inbound() }
        };

        foreach (var movement in repository.Movements)
        {
            repository.Available[new StockLockKey(
                movement.ItemId,
                movement.WarehouseId,
                movement.LotId,
                movement.UnitId)] = availableQuantity;
        }

        var user = new FakeCurrentUser
        {
            UserIdValue = ActorId,
            Authenticated = true,
            WarehouseAllowed = true
        };

        var idempotency = new FakeIdempotencyStore();
        var unitOfWork = new FakeUnitOfWork();
        var audit = new FakeStockAuditWriter();

        var service = new StockPostingService(
            repository,
            audit,
            idempotency,
            unitOfWork,
            user);

        return new TestContext(
            service,
            repository,
            user,
            idempotency,
            unitOfWork,
            audit);
    }

    [Fact]
    public async Task Approved_document_posts_ledger_and_updates_balance()
    {
        var context = CreateContext();

        var result = await context.Service.PostAsync(
            Command(),
            CancellationToken.None);

        Assert.Equal(DocumentId, result.DocumentId);
        Assert.Equal("POSTED", result.Status);
        Assert.Equal(2, result.Version);
        Assert.False(result.Replayed);

        Assert.Equal(1, context.Repository.ApplyLedgerCalls);
        Assert.Equal(1, context.Repository.UpdateBalanceCalls);
        Assert.Equal(1, context.Repository.MarkPostedCalls);
        Assert.Equal(1, context.Audit.WriteCalls);
        Assert.Equal(1, context.UnitOfWork.CommitCalls);
        Assert.Equal(0, context.UnitOfWork.RollbackCalls);
        Assert.Equal(1, context.Idempotency.SaveCompletedCalls);
    }

    [Fact]
    public async Task Outbound_movement_without_sufficient_stock_is_rejected()
    {
        var context = CreateContext(
            new[] { Outbound(15m) },
            availableQuantity: 10m);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Service.PostAsync(
                Command(),
                CancellationToken.None));

        Assert.Equal("INSUFFICIENT_STOCK", exception.Message);
        Assert.Equal(0, context.Repository.ApplyLedgerCalls);
        Assert.Equal(0, context.Repository.UpdateBalanceCalls);
        Assert.Equal(0, context.Repository.MarkPostedCalls);
        Assert.Equal(1, context.UnitOfWork.RollbackCalls);
    }

    [Fact]
    public async Task Negative_stock_override_requires_permission()
    {
        var context = CreateContext(
            new[] { Outbound(15m) },
            availableQuantity: 10m);

        context.User.Permissions.Clear();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => context.Service.PostAsync(
                Command(
                    overrideNegative: true,
                    reason: "Approved inventory adjustment"),
                CancellationToken.None));

        Assert.Equal(0, context.UnitOfWork.BeginCalls);
        Assert.Equal(0, context.Repository.ApplyLedgerCalls);
    }

    [Fact]
    public async Task Negative_stock_override_requires_a_reason()
    {
        var context = CreateContext(
            new[] { Outbound(15m) },
            availableQuantity: 10m);

        context.User.Permissions.Add("stock.allow_negative");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Service.PostAsync(
                Command(overrideNegative: true, reason: " "),
                CancellationToken.None));

        Assert.Equal(0, context.UnitOfWork.BeginCalls);
        Assert.Equal(0, context.Repository.ApplyLedgerCalls);
    }

    [Fact]
    public async Task Authorized_negative_stock_override_posts_with_reason()
    {
        var context = CreateContext(
            new[] { Outbound(15m) },
            availableQuantity: 10m);

        context.User.Permissions.Add("stock.allow_negative");

        var result = await context.Service.PostAsync(
            Command(
                overrideNegative: true,
                reason: "Approved inventory adjustment"),
            CancellationToken.None);

        Assert.Equal("POSTED", result.Status);
        Assert.True(context.Repository.LastOverrideAuthorized);
        Assert.Equal(
            "Approved inventory adjustment",
            context.Repository.LastOverrideReason);
        Assert.Equal(1, context.Repository.UpdateBalanceCalls);
    }

    [Fact]
    public async Task Completed_idempotency_request_replays_without_posting_again()
    {
        var context = CreateContext();

        var previous = new PostStockDocumentResult(
            DocumentId,
            "POSTED",
            2,
            false);

        context.Idempotency.Reservation = new IdempotencyReservation(
            Guid.NewGuid(),
            ActorId,
            "/api/v1/stock-documents/{id}/post",
            "acceptance-key-001",
            "request-hash",
            "COMPLETED",
            200,
            JsonSerializer.Serialize(previous));

        var result = await context.Service.PostAsync(
            Command(),
            CancellationToken.None);

        Assert.True(result.Replayed);
        Assert.Equal("POSTED", result.Status);
        Assert.Equal(0, context.Repository.DocumentExistsCalls);
        Assert.Equal(0, context.Repository.ApplyLedgerCalls);
        Assert.Equal(0, context.Repository.UpdateBalanceCalls);
        Assert.Equal(1, context.UnitOfWork.CommitCalls);
    }

    [Fact]
    public async Task Idempotency_key_conflict_rolls_back()
    {
        var context = CreateContext();
        context.Idempotency.Conflict = true;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Service.PostAsync(
                Command(),
                CancellationToken.None));

        Assert.Equal("IDEMPOTENCY_CONFLICT", exception.Message);
        Assert.Equal(1, context.UnitOfWork.RollbackCalls);
        Assert.Equal(0, context.Repository.ApplyLedgerCalls);
    }

    [Fact]
    public async Task User_outside_warehouse_scope_cannot_post_document()
    {
        var context = CreateContext();
        context.User.WarehouseAllowed = false;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => context.Service.PostAsync(
                Command(),
                CancellationToken.None));

        Assert.Equal(0, context.Repository.ApplyLedgerCalls);
        Assert.Equal(0, context.Repository.UpdateBalanceCalls);
        Assert.Equal(1, context.UnitOfWork.RollbackCalls);
    }

    [Fact]
    public async Task Version_conflict_rolls_back_posting_transaction()
    {
        var context = CreateContext();
        context.Repository.MarkPostedException =
            new InvalidOperationException("CONFLICT_VERSION");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Service.PostAsync(
                Command(expectedVersion: 99),
                CancellationToken.None));

        Assert.Equal("CONFLICT_VERSION", exception.Message);
        Assert.Equal(1, context.Repository.ApplyLedgerCalls);
        Assert.Equal(1, context.Repository.UpdateBalanceCalls);
        Assert.Equal(1, context.UnitOfWork.RollbackCalls);
        Assert.Equal(0, context.UnitOfWork.CommitCalls);
    }

    private sealed record TestContext(
        StockPostingService Service,
        FakeStockPostingRepository Repository,
        FakeCurrentUser User,
        FakeIdempotencyStore Idempotency,
        FakeUnitOfWork UnitOfWork,
        FakeStockAuditWriter Audit);

    private sealed class FakeStockPostingRepository
        : IStockPostingRepository
    {
        public IReadOnlyList<StockMovement> Movements { get; set; } =
            Array.Empty<StockMovement>();

        public Dictionary<StockLockKey, decimal> Available { get; } = new();

        public int DocumentExistsCalls { get; private set; }
        public int ApplyLedgerCalls { get; private set; }
        public int UpdateBalanceCalls { get; private set; }
        public int MarkPostedCalls { get; private set; }

        public bool LastOverrideAuthorized { get; private set; }
        public string? LastOverrideReason { get; private set; }

        public Exception? MarkPostedException { get; set; }

        public Task<bool> DocumentExistsAsync(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            DocumentExistsCalls++;
            return Task.FromResult(documentId == DocumentId);
        }

        public Task<bool> IsDocumentPostedAsync(
            Guid documentId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<StockMovement>> GetMovementsAsync(
            Guid documentId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Movements);

        public Task<IReadOnlyDictionary<StockLockKey, decimal>>
            GetAvailableQuantitiesAsync(
                IReadOnlyCollection<StockLockKey> lockKeys,
                CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<StockLockKey, decimal>>(
                Available);

        public Task AcquireStockLocksAsync(
            IReadOnlyCollection<StockLockKey> lockKeys,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<StockMovement>> ApplyLedgerEntriesAsync(
            Guid documentId,
            Guid actorId,
            IReadOnlyCollection<StockMovement> movements,
            CancellationToken cancellationToken)
        {
            ApplyLedgerCalls++;
            return Task.FromResult(movements.ToArray() as IReadOnlyList<StockMovement>);
        }

        public Task UpdateBalancesAsync(
            IReadOnlyCollection<StockMovement> movements,
            bool negativeOverrideAuthorized,
            string? negativeOverrideReason,
            CancellationToken cancellationToken)
        {
            UpdateBalanceCalls++;
            LastOverrideAuthorized = negativeOverrideAuthorized;
            LastOverrideReason = negativeOverrideReason;
            return Task.CompletedTask;
        }

        public Task MarkDocumentPostedAsync(
            Guid documentId,
            Guid actorId,
            long expectedVersion,
            CancellationToken cancellationToken)
        {
            MarkPostedCalls++;

            if (MarkPostedException is not null)
                throw MarkPostedException;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid? UserIdValue { get; set; }
        public bool Authenticated { get; set; }
        public bool WarehouseAllowed { get; set; }

        public HashSet<string> Permissions { get; } =
            new(StringComparer.Ordinal)
            {
                "stock.allow_negative"
            };

        public Guid? UserId => UserIdValue;
        public string? Username => "stock-posting-test";
        public bool IsAuthenticated => Authenticated;
        IReadOnlySet<string> ICurrentUser.Permissions => Permissions;
        public IReadOnlySet<Guid> WarehouseScopes => new HashSet<Guid>();
        public IReadOnlySet<Guid> FactoryScopes => new HashSet<Guid>();
        public IReadOnlySet<string> DepartmentScopes => new HashSet<string>();
        public bool HasRecentMfa => true;

        public bool HasPermission(string code) =>
            Permissions.Contains(code);

        public bool AllowsWarehouse(Guid warehouseId) =>
            WarehouseAllowed;

        public bool AllowsFactory(Guid factoryId) => true;
        public bool AllowsDepartment(string code) => true;
    }

    private sealed class FakeIdempotencyStore : IIdempotencyStore
    {
        public bool Conflict { get; set; }

        public IdempotencyReservation Reservation { get; set; } =
            new(
                Guid.NewGuid(),
                ActorId,
                "/api/v1/stock-documents/{id}/post",
                "acceptance-key-001",
                "request-hash",
                "IN_PROGRESS",
                null,
                null);

        public int SaveCompletedCalls { get; private set; }

        public Task<(IdempotencyReservation? completed, bool conflict)>
            ReserveOrReplayAsync(
                Guid userId,
                string routeKey,
                string key,
                string requestHash,
                TimeSpan retention,
                CancellationToken ct) =>
            Task.FromResult<(IdempotencyReservation?, bool)>(
                (Reservation, Conflict));

        public Task SaveCompletedAsync(
            Guid reservationId,
            int status,
            string body,
            CancellationToken ct)
        {
            SaveCompletedCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int BeginCalls { get; private set; }
        public int CommitCalls { get; private set; }
        public int RollbackCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct) =>
            Task.FromResult(0);

        public Task BeginAsync(CancellationToken ct)
        {
            BeginCalls++;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken ct)
        {
            CommitCalls++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken ct)
        {
            RollbackCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStockAuditWriter : IStockAuditWriter
    {
        public int WriteCalls { get; private set; }

        public Task WriteAsync(
            string action,
            Guid documentId,
            Guid actorId,
            string details,
            string? reason,
            CancellationToken cancellationToken)
        {
            WriteCalls++;
            return Task.CompletedTask;
        }
    }
}