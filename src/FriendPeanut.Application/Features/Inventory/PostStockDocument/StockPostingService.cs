using System.Text.Json;
using FriendPeanut.Application.Abstractions;
using FriendPeanut.Domain.Inventory;

namespace FriendPeanut.Application.Features.Inventory.PostStockDocument;

public sealed class StockPostingService
{
    private readonly IStockPostingRepository _repository;
    private readonly IStockAuditWriter _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public StockPostingService(
        IStockPostingRepository repository,
        IStockAuditWriter audit,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<PostStockDocumentResult> PostAsync(
        PostStockDocumentCommand command,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication is required.");

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            throw new InvalidOperationException("Idempotency-Key is required.");

        if (command.NegativeStockOverrideRequested)
        {
            if (!_currentUser.HasPermission("stock.allow_negative"))
                throw new UnauthorizedAccessException("Missing stock.allow_negative permission.");

            if (string.IsNullOrWhiteSpace(command.NegativeStockOverrideReason))
                throw new InvalidOperationException("A reason is required for a negative-stock override.");
        }

        await _unitOfWork.BeginAsync(cancellationToken);
        try
        {
            if (!await _repository.DocumentExistsAsync(command.DocumentId, cancellationToken))
                throw new InvalidOperationException("Stock document was not found.");

            if (await _repository.IsDocumentPostedAsync(command.DocumentId, cancellationToken))
            {
                await _unitOfWork.CommitAsync(cancellationToken);
                return new PostStockDocumentResult(command.DocumentId, "POSTED", command.ExpectedVersion, true);
            }

            var movements = await _repository.GetMovementsAsync(command.DocumentId, cancellationToken);
            if (movements.Count == 0)
                throw new InvalidOperationException("Only approved stock documents with lines can be posted.");

            foreach (var movement in movements)
            {
                StockPostingRules.ValidateMovement(movement);
                if (!_currentUser.AllowsWarehouse(movement.WarehouseId))
                    throw new UnauthorizedAccessException("The current user is outside the warehouse scope.");
            }

            var keys = movements
                .Select(x => new StockLockKey(x.ItemId, x.WarehouseId, x.LotId))
                .Distinct()
                .OrderBy(x => x.ItemId)
                .ThenBy(x => x.WarehouseId)
                .ThenBy(x => x.LotId)
                .ToArray();

            await _repository.AcquireStockLocksAsync(keys, cancellationToken);
            await _repository.ApplyLedgerEntriesAsync(command.DocumentId, movements, cancellationToken);
            await _repository.UpdateBalancesAsync(movements, cancellationToken);
            await _repository.MarkDocumentPostedAsync(command.DocumentId, command.ExpectedVersion, cancellationToken);

            var auditDetails = JsonSerializer.Serialize(new
            {
                ActorId = _currentUser.UserId,
                NegativeStockOverrideRequested = command.NegativeStockOverrideRequested,
                NegativeStockOverrideReason = command.NegativeStockOverrideReason,
                MovementCount = movements.Count
            });

            await _audit.WriteAsync(
                "STOCK_DOCUMENT_POSTED",
                command.DocumentId,
                auditDetails,
                cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            return new PostStockDocumentResult(
                command.DocumentId,
                "POSTED",
                command.ExpectedVersion + 1,
                false);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}