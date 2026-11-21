using System.Reflection;
using Xunit;

namespace FriendPeanut.ArchitectureTests;

public sealed class LayerTests
{
    private static readonly Assembly Domain = typeof(FriendPeanut.Domain.Common.Money).Assembly;
    private static readonly Assembly App = typeof(FriendPeanut.Application.Abstractions.ICurrentUser).Assembly;

    [Fact]
    public void Domain_has_no_external_dependencies()
    {
        var banned = new[] { "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "System.Net.Http", "FluentValidation", "Serilog" };
        var refs = Domain.GetReferencedAssemblies().Select(a => a.Name ?? "").ToArray();
        foreach (var b in banned) Assert.DoesNotContain(refs, r => r.StartsWith(b, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure_or_WebApi()
    {
        var refs = App.GetReferencedAssemblies().Select(a => a.Name ?? "").ToArray();
        Assert.DoesNotContain(refs, r => r.StartsWith("FriendPeanut.Infrastructure", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, r => r.StartsWith("FriendPeanut.WebApi", StringComparison.Ordinal));
    }
}