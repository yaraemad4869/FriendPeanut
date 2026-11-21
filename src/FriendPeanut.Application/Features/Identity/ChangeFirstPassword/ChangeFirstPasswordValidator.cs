using FluentValidation;

namespace FriendPeanut.Application.Features.Identity.ChangeFirstPassword;

public sealed class ChangeFirstPasswordValidator : AbstractValidator<ChangeFirstPasswordCommand>
{
    public ChangeFirstPasswordValidator()
    {
        RuleFor(x => x.PreAuthToken).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(256);
    }
}