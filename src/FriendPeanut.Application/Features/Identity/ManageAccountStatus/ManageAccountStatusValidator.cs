using FluentValidation;

namespace FriendPeanut.Application.Features.Identity.ManageAccountStatus;

public sealed class SetUserStatusValidator : AbstractValidator<SetUserStatusCommand>
{
    public SetUserStatusValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Status).Must(s => s is "ACTIVE" or "INACTIVE" or "LOCKED");
    }
}

public sealed class AdminResetPasswordValidator : AbstractValidator<AdminResetPasswordCommand>
{
    public AdminResetPasswordValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(256);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(600);
    }
}