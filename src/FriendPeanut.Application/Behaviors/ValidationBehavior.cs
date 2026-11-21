using FluentValidation;

namespace FriendPeanut.Application.Behaviors;

public static class ValidationBehavior
{
    public static async Task ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(request, ct);
        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray());
            throw new FriendPeanut.Application.Common.Validation.ValidationException(errors);
        }
    }
}