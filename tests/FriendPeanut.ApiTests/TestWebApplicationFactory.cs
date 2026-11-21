using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace FriendPeanut.ApiTests;

/// <summary>
/// Test-only WebApplicationFactory: content root = WebApi project dir,
/// environment = Testing, so Program.cs uses a dummy connection string
/// and the tests never need a running PostgreSQL.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(ResolveWebApiContentRoot());
    }

    private static string ResolveWebApiContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "FriendPeanut.WebApi");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate src/FriendPeanut.WebApi from the test output directory.");
    }
}