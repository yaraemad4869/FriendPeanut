using FluentValidation;

namespace FriendPeanut.Application.Features.MasterData.ManageItems;

public sealed class ItemCreateValidator : AbstractValidator<ItemCreateRequest>
{
    public ItemCreateValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9_-]+$");
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NormalizedName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}

public sealed class ItemUpdateValidator : AbstractValidator<ItemUpdateRequest>
{
    public ItemUpdateValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NormalizedName).NotEmpty().MaximumLength(200);
    }
}