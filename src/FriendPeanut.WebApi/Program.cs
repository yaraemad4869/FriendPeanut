using FriendPeanut.Application.Abstractions;
using FriendPeanut.Application.Common.Text;
using FriendPeanut.Application.Features.Identity.AuthenticateUser;
using FriendPeanut.Application.Features.Identity.ChangeFirstPassword;
using FriendPeanut.Application.Features.Identity.EnrollAndVerifyMfa;
using FriendPeanut.Application.Features.Identity.GetMyProfile;
using FriendPeanut.Application.Features.Identity.ManageAccountStatus;
using FriendPeanut.Application.Features.MasterData.ConvertQuantity;
using FriendPeanut.Application.Features.MasterData.ListItemCategories;
using FriendPeanut.Application.Features.MasterData.ListWarehouses;
using FriendPeanut.Application.Features.MasterData.ManageCostCenters;
using FriendPeanut.Application.Features.MasterData.ManageItems;
using FriendPeanut.Application.Features.MasterData.ManageLots;
using FriendPeanut.Application.Features.MasterData.ManageParties;
using FriendPeanut.Application.Features.MasterData.SearchItemAliases;
using FriendPeanut.Infrastructure.Identity;
using FriendPeanut.Infrastructure.Inventory;
using FriendPeanut.Application.Features.Inventory.PostStockDocument;
using FriendPeanut.WebApi.Endpoints.Inventory;
using FriendPeanut.Infrastructure.Persistence;
using FriendPeanut.Infrastructure.Text;
using FriendPeanut.WebApi.Authorization;
using FriendPeanut.WebApi.Endpoints;
using FriendPeanut.WebApi.Health;
using FriendPeanut.WebApi.Json;
using FriendPeanut.WebApi.Middleware;
using FriendPeanut.WebApi.Networking;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "FriendPeanut.BOS")
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.Configure<JsonOptions>(opt =>
{
    opt.SerializerOptions.Converters.Add(new DecimalAsStringConverter());
    opt.SerializerOptions.Converters.Add(new NullableDecimalAsStringConverter());
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IClock, FriendPeanut.Infrastructure.Identity.SystemClock>();
builder.Services.AddScoped<IPasswordHasher, AspNetCorePasswordHasher>();
builder.Services.AddSingleton<ITotpService, TotpService>();
builder.Services.AddSingleton<IMfaCrypto, MfaCrypto>();
builder.Services.AddScoped<IArabicNormalizer, ArabicNormalizer>();
builder.Services.AddScoped<IDeviceIdentityResolver, HeaderDeviceIdentityResolver>();

var connStr = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrEmpty(connStr))
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        connStr = "Host=localhost;Port=5432;Database=friend_peanut;Username=app_user;Password=unused_in_tests;Timeout=1";
    }
    else
    {
        throw new InvalidOperationException("ConnectionStrings:Default missing.");
    }
}
builder.Services.AddSingleton(sp => new NpgsqlDataSourceBuilder(connStr).Build());
builder.Services.AddDbContext<FriendPeanutDbContext>(opt => opt.UseNpgsql(connStr));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUserStore, NpgsqlUserStore>();
builder.Services.AddScoped<ISessionStore, NpgsqlSessionStore>();
builder.Services.AddScoped<IAuthChallengeStore, NpgsqlAuthChallengeStore>();
builder.Services.AddScoped<ILoginAttemptStore, NpgsqlLoginAttemptStore>();
builder.Services.AddScoped<IMfaCredentialStore, NpgsqlMfaCredentialStore>();
builder.Services.AddScoped<IPolicyDecisionStore, NpgsqlPolicyDecisionStore>();
builder.Services.AddScoped<IRoleStore, NpgsqlRoleStore>();
builder.Services.AddScoped<IScopeStore, NpgsqlScopeStore>();
builder.Services.AddScoped<IMasterDataStore, NpgsqlMasterDataStore>();
builder.Services.AddScoped<IStockPostingRepository, StockPostingRepository>();
builder.Services.AddScoped<IStockAuditWriter, NpgsqlStockAuditWriter>();
builder.Services.AddScoped<IIdempotencyStore, NpgsqlIdempotencyStore>();
builder.Services.AddScoped<StockPostingService>();

builder.Services.AddScoped<AuthenticateUserValidator>();
builder.Services.AddScoped<AuthenticateUserHandler>();
builder.Services.AddScoped<ChangeFirstPasswordValidator>();
builder.Services.AddScoped<ChangeFirstPasswordHandler>();
builder.Services.AddScoped<SetupMfaHandler>();
builder.Services.AddScoped<VerifyMfaHandler>();
builder.Services.AddScoped<GetMyProfileHandler>();
builder.Services.AddScoped<SetUserStatusValidator>();
builder.Services.AddScoped<AdminResetPasswordValidator>();
builder.Services.AddScoped<ManageAccountStatusHandler>();
builder.Services.AddScoped<ManageItemsHandler>();
builder.Services.AddScoped<ItemCreateValidator>();
builder.Services.AddScoped<ItemUpdateValidator>();
builder.Services.AddScoped<ListItemCategoriesHandler>();
builder.Services.AddScoped<ConvertQuantityHandler>();
builder.Services.AddScoped<ListWarehousesHandler>();
builder.Services.AddScoped<ManagePartiesHandler>();
builder.Services.AddScoped<ManageCostCentersHandler>();
builder.Services.AddScoped<ManageLotsHandler>();
builder.Services.AddScoped<SearchItemAliasesHandler>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opts =>
    {
        opts.Cookie.Name = "fp_session";
        opts.Cookie.HttpOnly = true;
        opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        opts.Cookie.SameSite = SameSiteMode.Strict;
        opts.SlidingExpiration = false;
        opts.ExpireTimeSpan = TimeSpan.FromHours(8);
        opts.Events.OnValidatePrincipal = async ctx =>
        {
            var token = ctx.Request.Cookies["fp_session"];
            if (string.IsNullOrEmpty(token))
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
            var store = ctx.HttpContext.RequestServices.GetRequiredService<ISessionStore>();
            var session = await store.FindByTokenHashAsync(hash, ctx.HttpContext.RequestAborted);
            if (session is null || session.RevokedAt is not null || session.ExpiresAt < DateTimeOffset.UtcNow)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddOpenApi();

var liveTags = new[] { "live" };
var readyTags = new[] { "ready" };
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: liveTags)
    .AddDbContextCheck<FriendPeanutDbContext>(tags: readyTags);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ProblemDetailsMiddleware>();
app.UseMiddleware<CsrfValidationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapMasterDataEndpoints();
app.MapStockPostingEndpoint();
app.MapIdentityAdminEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();

public partial class Program { }