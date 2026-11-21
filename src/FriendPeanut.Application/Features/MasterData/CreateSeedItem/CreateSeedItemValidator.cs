using FluentValidation;
using FriendPeanut.Application.Features.MasterData.ManageItems;

namespace FriendPeanut.Application.Features.MasterData.CreateSeedItem;

public sealed class CreateSeedItemValidator : AbstractValidator<ItemCreateRequest>
{
    public CreateSeedItemValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}