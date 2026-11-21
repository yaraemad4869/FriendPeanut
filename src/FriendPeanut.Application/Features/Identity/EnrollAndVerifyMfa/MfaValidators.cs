using FluentValidation;

namespace FriendPeanut.Application.Features.Identity.EnrollAndVerifyMfa;

public sealed class VerifyMfaValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(6).Matches("^[0-9]{6}$");
    }
}