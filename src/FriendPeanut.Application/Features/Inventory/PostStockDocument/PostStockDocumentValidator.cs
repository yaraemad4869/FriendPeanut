using FluentValidation;

namespace FriendPeanut.Application.Features.Inventory.PostStockDocument;

public sealed class PostStockDocumentValidator
    : AbstractValidator<PostStockDocumentCommand>
{
    public PostStockDocumentValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty();

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0);

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.NegativeStockOverrideReason)
            .NotEmpty()
            .When(x => x.NegativeStockOverrideRequested);
    }
}
