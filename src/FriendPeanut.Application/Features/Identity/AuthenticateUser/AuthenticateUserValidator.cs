using FluentValidation;

namespace FriendPeanut.Application.Features.Identity.AuthenticateUser;

public sealed class AuthenticateUserValidator : AbstractValidator<AuthenticateUserCommand>
{
    public AuthenticateUserValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(256);
    }
}