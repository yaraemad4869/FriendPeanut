using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace FriendPeanut.ApiTests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(ResolveWebApiContentRoot());
    }

    private static string ResolveWebApiContentRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("FP_WEBAPI_CONTENT_ROOT");

        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            var fullConfiguredRoot = Path.GetFullPath(configuredRoot);
            if (Directory.Exists(fullConfiguredRoot))
                return fullConfiguredRoot;
        }

        var searchRoots = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var searchRoot in searchRoots)
        {
            var dir = new DirectoryInfo(searchRoot);

            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "src", "FriendPeanut.WebApi");
                if (Directory.Exists(candidate))
                    return candidate;

                dir = dir.Parent;
            }
        }

        throw new InvalidOperationException(
            "Could not locate src/FriendPeanut.WebApi. Set FP_WEBAPI_CONTENT_ROOT to the WebApi project directory.");
    }
}
