using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FriendPeanut.Application.Abstractions;
using FriendPeanut.Domain.Inventory;

namespace FriendPeanut.Application.Features.Inventory.PostStockDocument;

public sealed class StockPostingService
{
    private const string RouteKey = "/api/v1/stock-documents/{id}/post";

    private readonly IStockPostingRepository _repository;
    private readonly IStockAuditWriter _audit;
    private readonly IIdempotencyStore _idempotency;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public StockPostingService(
        IStockPostingRepository repository,
        IStockAuditWriter audit,
        IIdempotencyStore idempotency,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _audit = audit;
        _idempotency = idempotency;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<PostStockDocumentResult> PostAsync(
        PostStockDocumentCommand command,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException(
                "Authentication is required.");

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            throw new InvalidOperationException(
                "Idempotency-Key is required.");

        if (command.IdempotencyKey.Length > 100)
            throw new InvalidOperationException(
                "Idempotency-Key exceeds 100 characters.");

        if (command.NegativeStockOverrideRequested)
        {
            if (!_currentUser.HasPermission("stock.allow_negative"))
                throw new UnauthorizedAccessException(
                    "Missing stock.allow_negative permission.");

            if (string.IsNullOrWhiteSpace(command.NegativeStockOverrideReason))
                throw new InvalidOperationException(
                    "A reason is required for a negative-stock override.");

            if (command.NegativeStockOverrideReason.Length > 600)
                throw new InvalidOperationException(
                    "Negative-stock override reason exceeds 600 characters.");
        }

        var actorId = _currentUser.UserId.Value;

        var requestPayload = JsonSerializer.Serialize(new
        {
            command.DocumentId,
            command.ExpectedVersion,
            command.NegativeStockOverrideRequested,
            command.NegativeStockOverrideReason
        });

        var requestHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(requestPayload)))
            .ToLowerInvariant();

        await _unitOfWork.BeginAsync(cancellationToken);

        try
        {
            var (reservation, conflict) =
                await _idempotency.ReserveOrReplayAsync(
                    actorId,
                    RouteKey,
                    command.IdempotencyKey,
                    requestHash,
                    TimeSpan.FromHours(24),
                    cancellationToken);

            if (conflict)
                throw new InvalidOperationException("IDEMPOTENCY_CONFLICT");

            if (reservation is null)
                throw new InvalidOperationException(
                    "Idempotency reservation could not be created.");

            if (reservation.State == "COMPLETED")
            {
                if (string.IsNullOrWhiteSpace(reservation.ResponseBody))
                    throw new InvalidOperationException(
                        "Completed idempotency response is missing.");

                var previous =
                    JsonSerializer.Deserialize<PostStockDocumentResult>(
                        reservation.ResponseBody);

                if (previous is null)
                    throw new InvalidOperationException(
                        "Completed idempotency response is invalid.");

                await _unitOfWork.CommitAsync(cancellationToken);

                return new PostStockDocumentResult(
                    previous.DocumentId,
                    previous.Status,
                    previous.Version,
                    true);
            }

            if (reservation.State != "IN_PROGRESS")
                throw new InvalidOperationException(
                    "Unexpected idempotency reservation state.");

            if (!await _repository.DocumentExistsAsync(
                command.DocumentId,
                cancellationToken))
            {
                throw new InvalidOperationException(
                    "Stock document was not found.");
            }

            if (await _repository.IsDocumentPostedAsync(
                command.DocumentId,
                cancellationToken))
            {
                var alreadyPosted = new PostStockDocumentResult(
                    command.DocumentId,
                    "POSTED",
                    command.ExpectedVersion + 1,
                    true);

                await _idempotency.SaveCompletedAsync(
                    reservation.Id,
                    200,
                    JsonSerializer.Serialize(alreadyPosted),
                    cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);
                return alreadyPosted;
            }

            var movements = await _repository.GetMovementsAsync(
                command.DocumentId,
                cancellationToken);

            if (movements.Count == 0)
                throw new InvalidOperationException(
                    "Only approved stock documents with lines can be posted.");

            foreach (var movement in movements)
            {
                StockPostingRules.ValidateMovement(movement);

                if (!_currentUser.AllowsWarehouse(movement.WarehouseId))
                    throw new UnauthorizedAccessException(
                        "The current user is outside the warehouse scope.");
            }

            var lockKeys = movements
                .Select(x => new StockLockKey(
                    x.ItemId,
                    x.WarehouseId,
                    x.LotId,
                    x.UnitId))
                .Distinct()
                .OrderBy(x => x.ItemId)
                .ThenBy(x => x.WarehouseId)
                .ThenBy(x => x.LotId)
                .ThenBy(x => x.UnitId)
                .ToArray();

            await _repository.AcquireStockLocksAsync(
                lockKeys,
                cancellationToken);

            var available =
                await _repository.GetAvailableQuantitiesAsync(
                    lockKeys,
                    cancellationToken);

            foreach (var group in movements.GroupBy(x => new StockLockKey(
                x.ItemId, x.WarehouseId, x.LotId, x.UnitId)))
            {
                if (!available.TryGetValue(group.Key, out var currentQuantity))
                    throw new InvalidOperationException(
                        "Available stock balance could not be resolved.");

                var resultingQuantity =
                    currentQuantity + group.Sum(x => x.SignedQuantity);

                if (resultingQuantity < 0 &&
                    !command.NegativeStockOverrideRequested)
                {
                    throw new InvalidOperationException(
                        "INSUFFICIENT_STOCK");
                }
            }

            var insertedMovements =
                await _repository.ApplyLedgerEntriesAsync(
                    command.DocumentId,
                    actorId,
                    movements,
                    cancellationToken);

            await _repository.UpdateBalancesAsync(
                insertedMovements,
                command.NegativeStockOverrideRequested,
                command.NegativeStockOverrideReason,
                cancellationToken);

            await _repository.MarkDocumentPostedAsync(
                command.DocumentId,
                actorId,
                command.ExpectedVersion,
                cancellationToken);

            var auditDetails = JsonSerializer.Serialize(new
            {
                ActorId = actorId,
                NegativeStockOverrideRequested =
                    command.NegativeStockOverrideRequested,
                NegativeStockOverrideReason =
                    command.NegativeStockOverrideReason,
                MovementCount = movements.Count,
                NewlyInsertedLedgerEntries = insertedMovements.Count
            });

            await _audit.WriteAsync(
                "STOCK_DOCUMENT_POSTED",
                command.DocumentId,
                actorId,
                auditDetails,
                command.NegativeStockOverrideReason,
                cancellationToken);

            var result = new PostStockDocumentResult(
                command.DocumentId,
                "POSTED",
                command.ExpectedVersion + 1,
                false);

            await _idempotency.SaveCompletedAsync(
                reservation.Id,
                200,
                JsonSerializer.Serialize(result),
                cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            try
            {
                await _unitOfWork.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Preserve the original exception.
            }

            throw;
        }
    }
}